#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"
etl_project="${repo_root}/backend/OlympiaStudent360.CanvasEtl/OlympiaStudent360.CanvasEtl.csproj"

mysql_bin="${MYSQL_BIN:-$(command -v mysql || true)}"
mysqldump_bin="${MYSQLDUMP_BIN:-$(command -v mysqldump || true)}"
if [[ -z "${mysql_bin}" || -z "${mysqldump_bin}" ]]; then
  echo "mysql and mysqldump must be installed and available on PATH." >&2
  exit 1
fi

connection_string="${ConnectionStrings__DefaultConnection:-}"
if [[ -z "${connection_string}" ]]; then
  connection_string="$(dotnet user-secrets list --project "${etl_project}" 2>/dev/null \
    | sed -n 's/^ConnectionStrings:DefaultConnection = //p' | head -n 1)"
fi
if [[ -z "${connection_string}" ]]; then
  echo "ConnectionStrings:DefaultConnection is not configured for the Canvas ETL." >&2
  exit 1
fi

trim() {
  local value="$1"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  printf '%s' "${value}"
}

db_host=""
db_port="3306"
db_name=""
db_user=""
db_password=""
IFS=';' read -r -a connection_parts <<< "${connection_string}"
for part in "${connection_parts[@]}"; do
  [[ "${part}" == *"="* ]] || continue
  key="$(trim "${part%%=*}")"
  value="$(trim "${part#*=}")"
  key="$(printf '%s' "${key}" | tr '[:upper:]' '[:lower:]')"
  if [[ "${value}" == \"*\" && "${value}" == *\" ]]; then
    value="${value:1:${#value}-2}"
  fi
  case "${key}" in
    server|host|data\ source) db_host="${value}" ;;
    port) db_port="${value}" ;;
    database|initial\ catalog) db_name="${value}" ;;
    user|user\ id|uid|username) db_user="${value}" ;;
    password|pwd) db_password="${value}" ;;
  esac
done

if [[ -z "${db_host}" || -z "${db_name}" || -z "${db_user}" ]]; then
  echo "The configured MySQL connection string is missing server, database, or user." >&2
  exit 1
fi
if [[ ! "${db_name}" =~ ^[A-Za-z0-9_$-]+$ ]] || [[ ! "${db_port}" =~ ^[0-9]+$ ]]; then
  echo "The configured MySQL database name or port is not safe to pass to the client." >&2
  exit 1
fi

mysql_args=(--protocol=TCP --host="${db_host}" --port="${db_port}" --user="${db_user}" \
  --default-character-set=utf8mb4 --batch --skip-column-names "${db_name}")
mysql_local() {
  MYSQL_PWD="${db_password}" "${mysql_bin}" "${mysql_args[@]}" "$@"
}

state_row="$(mysql_local --execute="
  SELECT CONCAT(COALESCE(LastStatus, ''), '|',
    COALESCE(DATE_FORMAT(LastSuccessfulWatermarkUtc, '%Y%m%dT%H%i%sZ'), ''))
  FROM CanvasSyncStates
  ORDER BY LastSuccessfulWatermarkUtc DESC
  LIMIT 1;")"
state_status="${state_row%%|*}"
watermark_slug="${state_row#*|}"
running_count="$(mysql_local --execute="SELECT COUNT(*) FROM CanvasSyncRuns WHERE Status = 'running';")"
if [[ "${state_status}" != "completed" || -z "${watermark_slug}" || "${running_count}" != "0" ]]; then
  echo "Canvas export requires a completed sync state, a successful watermark, and no running ETL." >&2
  exit 1
fi

output_path="${1:-${script_dir}/generated/canvas-rds-upsert-${watermark_slug}.sql}"
mkdir -p "$(dirname "${output_path}")"
temporary_path="$(mktemp "${output_path}.tmp.XXXXXX")"
trap 'rm -f "${temporary_path}"' EXIT
chmod 600 "${temporary_path}"

tables=(CanvasAccounts CanvasUsers CanvasCourses CanvasCourseSections CanvasAssignments \
  CanvasEnrollments CanvasCourseScores CanvasSubmissions CanvasSyncStates CanvasSyncRuns)

write_line() {
  printf '%s\n' "$1" >> "${temporary_path}"
}

write_line "-- Olympia Student 360 Canvas warehouse upsert for AWS RDS"
write_line "-- Source watermark: ${watermark_slug}"
write_line "-- Generated from a completed local Canvas ETL. Contains private student data; do not commit."
write_line "-- This is an insert/update transfer. It intentionally does not delete target rows."
write_line "-- Canonical transcript subjects are resolved by application code; no Blueprint relationships are included."
write_line "SET NAMES utf8mb4;"
write_line "SET time_zone = '+00:00';"
write_line ""

for table in "${tables[@]}"; do
  stage="_CanvasImport_${table}"
  write_line "DROP TEMPORARY TABLE IF EXISTS \`${stage}\`;"
  write_line "CREATE TEMPORARY TABLE \`${stage}\` AS SELECT * FROM \`${table}\` WHERE 1 = 0;"
done

submission_bounds="$(mysql_local --execute="SELECT COALESCE(MIN(Id), 0), COALESCE(MAX(Id), -1) FROM CanvasSubmissions;")"
submission_min_id="${submission_bounds%%$'\t'*}"
submission_max_id="${submission_bounds##*$'\t'}"
submission_batch_size=5000
if [[ "${submission_max_id}" -ge "${submission_min_id}" ]]; then
  write_line 'ALTER TABLE `_CanvasImport_CanvasSubmissions` ADD INDEX `_ix_CanvasImport_Submission_Id` (`Id`);'
  write_line ""
fi
write_line ""

dump_args=(--protocol=TCP --host="${db_host}" --port="${db_port}" --user="${db_user}"
  --default-character-set=utf8mb4 --single-transaction --quick --skip-lock-tables
  --no-create-info --skip-triggers --compact --complete-insert --extended-insert
  --hex-blob --order-by-primary --max-allowed-packet=1G --set-gtid-purged=OFF
  --no-tablespaces "${db_name}")

for table in "${tables[@]}"; do
  stage="_CanvasImport_${table}"
  write_line "-- Local snapshot: ${table}"
  MYSQL_PWD="${db_password}" "${mysqldump_bin}" "${dump_args[@]}" "${table}" \
    | sed -n "s/^INSERT INTO \`${table}\`/INSERT INTO \`${stage}\`/p" \
    >> "${temporary_path}"
  write_line ""
done

column_list() {
  local table="$1"
  local omit_student_id="$2"
  mysql_local --execute="
    SET SESSION group_concat_max_len = 100000;
    SELECT GROUP_CONCAT(CONCAT('\`', COLUMN_NAME, '\`') ORDER BY ORDINAL_POSITION SEPARATOR ', ')
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '${table}' AND COLUMN_NAME <> 'Id'
      AND ('${omit_student_id}' = 'false' OR COLUMN_NAME <> 'StudentProfileId');"
}

select_list() {
  local table="$1"
  local omit_student_id="$2"
  mysql_local --execute="
    SET SESSION group_concat_max_len = 100000;
    SELECT GROUP_CONCAT(CONCAT('s.\`', COLUMN_NAME, '\`') ORDER BY ORDINAL_POSITION SEPARATOR ', ')
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '${table}' AND COLUMN_NAME <> 'Id'
      AND ('${omit_student_id}' = 'false' OR COLUMN_NAME <> 'StudentProfileId');"
}

update_list() {
  local table="$1"
  local natural_key="$2"
  local omit_student_id="$3"
  mysql_local --execute="
    SET SESSION group_concat_max_len = 100000;
    SELECT GROUP_CONCAT(CONCAT('\`', COLUMN_NAME, '\` = VALUES(\`', COLUMN_NAME, '\`)')
      ORDER BY ORDINAL_POSITION SEPARATOR ', ')
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '${table}'
      AND COLUMN_NAME NOT IN ('Id', '${natural_key}')
      AND ('${omit_student_id}' = 'false' OR COLUMN_NAME <> 'StudentProfileId');"
}

merge_table() {
  local table="$1"
  local natural_key="$2"
  local omit_student_id="$3"
  local columns selects updates
  columns="$(column_list "${table}" "${omit_student_id}")"
  selects="$(select_list "${table}" "${omit_student_id}")"
  updates="$(update_list "${table}" "${natural_key}" "${omit_student_id}")"
  if [[ -z "${columns}" || -z "${selects}" || -z "${updates}" ]]; then
    echo "Could not read merge columns for ${table}." >&2
    exit 1
  fi
  write_line "START TRANSACTION;"
  write_line "INSERT INTO \`${table}\` (${columns})"
  write_line "SELECT ${selects} FROM \`_CanvasImport_${table}\` AS s WHERE TRUE"
  write_line "ON DUPLICATE KEY UPDATE ${updates};"
  write_line "COMMIT;"
  write_line ""
}

merge_submissions_in_batches() {
  local columns selects updates batch_start batch_end
  columns="$(column_list CanvasSubmissions true)"
  selects="$(select_list CanvasSubmissions true)"
  updates="$(update_list CanvasSubmissions CanvasSubmissionId true)"
  batch_start="${submission_min_id}"
  while [[ "${batch_start}" -le "${submission_max_id}" ]]; do
    batch_end=$((batch_start + submission_batch_size - 1))
    write_line "START TRANSACTION;"
    write_line "INSERT INTO \`CanvasSubmissions\` (${columns})"
    write_line "SELECT ${selects} FROM \`_CanvasImport_CanvasSubmissions\` AS s WHERE s.\`Id\` BETWEEN ${batch_start} AND ${batch_end}"
    write_line "ON DUPLICATE KEY UPDATE ${updates};"
    write_line "COMMIT;"
    write_line ""
    batch_start=$((batch_end + 1))
  done
}

write_line "-- Stable-identity upserts; local auto-increment IDs are never copied."
write_line "-- Retain report invalidations even if a later transfer batch fails."
write_line 'INSERT INTO `CanvasReportCourseChanges` (`CanvasCourseId`, `ChangedAtUtc`)'
write_line 'SELECT `CanvasCourseId`, UTC_TIMESTAMP(6) FROM `_CanvasImport_CanvasCourses` WHERE TRUE'
write_line 'ON DUPLICATE KEY UPDATE `ChangedAtUtc` = UTC_TIMESTAMP(6);'
merge_table CanvasAccounts CanvasAccountId false
merge_table CanvasUsers CanvasUserId false
merge_table CanvasCourses CanvasCourseId false
merge_table CanvasCourseSections CanvasSectionId false
merge_table CanvasAssignments CanvasAssignmentId false
merge_table CanvasEnrollments CanvasEnrollmentId true
merge_table CanvasCourseScores WarehouseScoreFactId true
merge_submissions_in_batches

write_line "-- Abort this statement on conflicting Canvas/SIS identity links. Later relinks are also conflict-safe."
write_line 'CREATE TEMPORARY TABLE `_CanvasImportIdentityGuard` (`MustBeOne` TINYINT NOT NULL CHECK (`MustBeOne` = 1));'
write_line 'INSERT INTO `_CanvasImportIdentityGuard` (`MustBeOne`)'
write_line 'SELECT 0 FROM `'_CanvasImport_CanvasUsers'` u'
write_line 'INNER JOIN `StudentProfiles` pSis'
write_line "  ON LOWER(TRIM(pSis.\`SISUserId\`)) = LOWER(SUBSTRING_INDEX(TRIM(COALESCE(NULLIF(u.\`SisUserId\`, ''), NULLIF(u.\`LoginId\`, ''), u.\`Email\`)), '@', 1))"
write_line 'LEFT JOIN `StudentProfiles` pCanvas ON pCanvas.`CanvasUserId` = u.`CanvasUserId`'
write_line 'WHERE (pSis.`CanvasUserId` IS NOT NULL AND pSis.`CanvasUserId` <> u.`CanvasUserId`)'
write_line '   OR (pCanvas.`Id` IS NOT NULL AND pCanvas.`Id` <> pSis.`Id`)'
write_line 'LIMIT 1;'
write_line 'DROP TEMPORARY TABLE `_CanvasImportIdentityGuard`;'
write_line ""

write_line "-- Target-owned StudentProfile IDs are resolved on RDS rather than copied from local MySQL."
write_line 'UPDATE `StudentProfiles` p'
write_line 'INNER JOIN `'_CanvasImport_CanvasUsers'` u'
write_line "  ON LOWER(TRIM(p.\`SISUserId\`)) = LOWER(SUBSTRING_INDEX(TRIM(COALESCE(NULLIF(u.\`SisUserId\`, ''), NULLIF(u.\`LoginId\`, ''), u.\`Email\`)), '@', 1))"
write_line 'LEFT JOIN `StudentProfiles` pOther ON pOther.`CanvasUserId` = u.`CanvasUserId` AND pOther.`Id` <> p.`Id`'
write_line 'SET p.`CanvasUserId` = u.`CanvasUserId`, p.`UpdatedAt` = UTC_TIMESTAMP(6)'
write_line 'WHERE (p.`CanvasUserId` IS NULL OR p.`CanvasUserId` = u.`CanvasUserId`) AND pOther.`Id` IS NULL;'
write_line ""

write_line 'UPDATE `CanvasEnrollments` e'
write_line 'INNER JOIN `'_CanvasImport_CanvasEnrollments'` staged ON staged.`CanvasEnrollmentId` = e.`CanvasEnrollmentId`'
write_line 'LEFT JOIN `StudentProfiles` pCanvas ON pCanvas.`CanvasUserId` = e.`CanvasUserId`'
write_line "LEFT JOIN \`StudentProfiles\` pSis ON LOWER(TRIM(pSis.\`SISUserId\`)) = LOWER(SUBSTRING_INDEX(TRIM(e.\`SisUserId\`), '@', 1))"
write_line 'SET e.`StudentProfileId` = COALESCE(pCanvas.`Id`, pSis.`Id`), e.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)'
write_line 'WHERE pCanvas.`Id` IS NULL OR pSis.`Id` IS NULL OR pCanvas.`Id` = pSis.`Id`;'
write_line ""

write_line 'UPDATE `CanvasCourseScores` score'
write_line 'INNER JOIN `'_CanvasImport_CanvasCourseScores'` staged ON staged.`WarehouseScoreFactId` = score.`WarehouseScoreFactId`'
write_line 'INNER JOIN `CanvasEnrollments` e ON e.`CanvasEnrollmentId` = score.`CanvasEnrollmentId`'
write_line 'SET score.`StudentProfileId` = e.`StudentProfileId`, score.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6);'
write_line ""

batch_start="${submission_min_id}"
while [[ "${batch_start}" -le "${submission_max_id}" ]]; do
  batch_end=$((batch_start + submission_batch_size - 1))
  write_line "START TRANSACTION;"
  write_line 'UPDATE `CanvasSubmissions` submission'
  write_line 'INNER JOIN `'_CanvasImport_CanvasSubmissions'` staged ON staged.`CanvasSubmissionId` = submission.`CanvasSubmissionId`'
  write_line 'LEFT JOIN `StudentProfiles` pCanvas ON pCanvas.`CanvasUserId` = submission.`CanvasUserId`'
  write_line 'LEFT JOIN `CanvasUsers` u ON u.`CanvasUserId` = submission.`CanvasUserId`'
  write_line "LEFT JOIN \`StudentProfiles\` pSis ON LOWER(TRIM(pSis.\`SISUserId\`)) = LOWER(SUBSTRING_INDEX(TRIM(COALESCE(NULLIF(u.\`SisUserId\`, ''), NULLIF(u.\`LoginId\`, ''), u.\`Email\`)), '@', 1))"
  write_line 'SET submission.`StudentProfileId` = COALESCE(pCanvas.`Id`, pSis.`Id`), submission.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)'
  write_line "WHERE staged.\`Id\` BETWEEN ${batch_start} AND ${batch_end}"
  write_line '  AND (pCanvas.`Id` IS NULL OR pSis.`Id` IS NULL OR pCanvas.`Id` = pSis.`Id`);'
  write_line "COMMIT;"
  write_line ""
  batch_start=$((batch_end + 1))
done

write_line "-- Recompute the Canvas-only grade fallback using the same rule as the ETL."
write_line 'UPDATE `StudentProfiles` p'
write_line 'LEFT JOIN ('
write_line '  SELECT ranked.`StudentProfileId`, ranked.`CanvasGradeLevel`'
write_line '  FROM ('
write_line '    SELECT memberships.`StudentProfileId`, memberships.`CanvasGradeLevel`,'
write_line '      ROW_NUMBER() OVER (PARTITION BY memberships.`StudentProfileId`'
write_line '        ORDER BY memberships.`CourseCount` DESC,'
write_line "          CASE WHEN memberships.\`CanvasGradeLevel\` REGEXP '^[0-9]+$' THEN CAST(memberships.\`CanvasGradeLevel\` AS UNSIGNED) ELSE 0 END DESC) AS \`row_number\`"
write_line '    FROM ('
write_line '      SELECT e.`StudentProfileId`, c.`CanvasGradeLevel`, COUNT(*) AS `CourseCount`'
write_line '      FROM `CanvasEnrollments` e'
write_line '      INNER JOIN `CanvasCourses` c ON c.`CanvasCourseId` = e.`CanvasCourseId`'
write_line "      WHERE e.\`StudentProfileId\` IS NOT NULL AND c.\`CanvasGradeLevel\` IS NOT NULL AND c.\`CanvasGradeLevel\` <> ''"
write_line '      GROUP BY e.`StudentProfileId`, c.`CanvasGradeLevel`'
write_line '    ) memberships'
write_line '  ) ranked'
write_line '  WHERE ranked.`row_number` = 1'
write_line ') best ON best.`StudentProfileId` = p.`Id`'
write_line 'SET p.`CanvasGradeLevel` = best.`CanvasGradeLevel`, p.`UpdatedAt` = UTC_TIMESTAMP(6)'
write_line 'WHERE p.`CanvasUserId` IS NOT NULL AND NOT (p.`CanvasGradeLevel` <=> best.`CanvasGradeLevel`);'
write_line ""

run_columns="$(column_list CanvasSyncRuns false)"
run_selects="$(select_list CanvasSyncRuns false)"
write_line "-- Preserve terminal run audit records without copying local auto-increment IDs."
write_line "INSERT INTO \`CanvasSyncRuns\` (${run_columns})"
write_line "SELECT ${run_selects} FROM \`_CanvasImport_CanvasSyncRuns\` s"
write_line "WHERE s.\`Status\` <> 'running' AND NOT EXISTS ("
write_line '  SELECT 1 FROM `CanvasSyncRuns` existing'
write_line '  WHERE existing.`SourceKey` = s.`SourceKey` AND existing.`StartedAtUtc` = s.`StartedAtUtc`'
write_line ');'
write_line ""

write_line "-- Fail before advancing the watermark if a stable Canvas identity was not merged."
write_line 'CREATE TEMPORARY TABLE `_CanvasImportMergeGuard` (`MustBeOne` TINYINT NOT NULL CHECK (`MustBeOne` = 1));'
write_line 'INSERT INTO `_CanvasImportMergeGuard` (`MustBeOne`)'
write_line 'SELECT 0 FROM ('
write_line '  SELECT s.`CanvasAccountId` FROM `'_CanvasImport_CanvasAccounts'` s LEFT JOIN `CanvasAccounts` t USING (`CanvasAccountId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasUserId` FROM `'_CanvasImport_CanvasUsers'` s LEFT JOIN `CanvasUsers` t USING (`CanvasUserId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasCourseId` FROM `'_CanvasImport_CanvasCourses'` s LEFT JOIN `CanvasCourses` t USING (`CanvasCourseId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasSectionId` FROM `'_CanvasImport_CanvasCourseSections'` s LEFT JOIN `CanvasCourseSections` t USING (`CanvasSectionId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasAssignmentId` FROM `'_CanvasImport_CanvasAssignments'` s LEFT JOIN `CanvasAssignments` t USING (`CanvasAssignmentId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasEnrollmentId` FROM `'_CanvasImport_CanvasEnrollments'` s LEFT JOIN `CanvasEnrollments` t USING (`CanvasEnrollmentId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`WarehouseScoreFactId` FROM `'_CanvasImport_CanvasCourseScores'` s LEFT JOIN `CanvasCourseScores` t USING (`WarehouseScoreFactId`) WHERE t.`Id` IS NULL'
write_line '  UNION ALL SELECT s.`CanvasSubmissionId` FROM `'_CanvasImport_CanvasSubmissions'` s LEFT JOIN `CanvasSubmissions` t USING (`CanvasSubmissionId`) WHERE t.`Id` IS NULL'
write_line ') missing LIMIT 1;'
write_line 'DROP TEMPORARY TABLE `_CanvasImportMergeGuard`;'
write_line ""

merge_table CanvasSyncStates SourceKey false
write_line "SELECT 'Canvas warehouse upsert committed' AS Result, '${watermark_slug}' AS SourceWatermark;"

mv "${temporary_path}" "${output_path}"
chmod 600 "${output_path}"
trap - EXIT
echo "Created ${output_path}"
