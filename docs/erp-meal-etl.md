# ERP student meals warehouse

The ERP API is not available yet. This setup adds the database schema, a shared
upsert and student-linking service, and an optional adapter for the three current
Excel exports. It does not schedule a sync or guess ERP API endpoints.

## Source mapping

| ERP export | Warehouse table | Feed | Fields |
| --- | --- | --- | --- |
| `DS_hoc_sinh_khong_in_phieu_an_sang_hoac_trua_THCS.xlsx` | `ErpMealStatuses` | `missing_ticket` | date, ERP student ID, name, email, class code, meal, order status, ticket status |
| `Bao_cao_hoc_sinh_khong_dat_hoac_quen_dat_an_THCS.xlsx` | `ErpMealStatuses` | `missing_order` | same columns |
| `Danh_sach_hoc_sinh_mua_hang_tai_canteen.xlsx` | `ErpCanteenPurchases` | `canteen` | date, ERP student ID, name, class code, product code/name, quantity |

`ErpMealSyncRuns` records the source, row totals, identity exceptions, and
completion or failure for each import. The two meal reports overlap; reporting
must choose a feed or explicitly reconcile them before combining counts.

The ERP `ID học sinh` is matched directly to `StudentProfiles.SISStudentCode`
after trimming and case normalization. For example, `OS002122-12` links to the
profile whose `SISStudentCode` is `OS002122-12`; the warehouse row stores that
profile's `Id` in `StudentProfileId`. An ambiguous duplicate normalized code
remains unlinked with `MatchStatus=conflict`. A code absent from the SIS roster
remains in the warehouse with `MatchStatus=unmatched`, allowing later review
without losing the ERP row. Email, names, and class codes are retained as
source data but never establish identity.

The current exports contain 19,797 canteen rows, 11,241 missing-ticket rows,
and 12,794 missing-order rows. Two canteen rows have blank product code and name;
the nullable columns preserve them. Repeated student/date/meal and
student/date/product combinations occur, so neither is a row identifier.

## Run the export adapter

The API and the ETL CLI both apply EF migrations. A database connection must
be provided through `ConnectionStrings__DefaultConnection` or ETL user secrets.
Use `--dry-run` to parse and validate without connecting to a database:

```bash
dotnet run --project backend/OlympiaStudent360.ErpMealEtl -- \
  --canteen='/path/to/Danh_sach_hoc_sinh_mua_hang_tai_canteen.xlsx' \
  --missing-ticket='/path/to/DS_hoc_sinh_khong_in_phieu_an_sang_hoac_trua_THCS.xlsx' \
  --missing-order='/path/to/Bao_cao_hoc_sinh_khong_dat_hoac_quen_dat_an_THCS.xlsx' \
  --dry-run
```

Remove `--dry-run` to import. Any of the three feeds can be supplied alone.
Run `dotnet run --project backend/OlympiaStudent360.ErpMealEtl -- --relink-only`
after the SIS roster gains students that were previously unmatched.
Reimporting an identical workbook updates its rows: the export adapter makes
`SourceRecordKey` from a file SHA-256 fingerprint and Excel row number. A newly
generated workbook with different bytes may create new rows even if some
business events recur; use the ERP's immutable record IDs once its API is
available. Do not use a student/date/meal or student/date/product key, because
the supplied reports contain valid repeated rows.

## Connecting the ERP API later

Create an API adapter that fetches each feed, maps it to
`ErpMealStatusInput` or `ErpCanteenPurchaseInput`, and calls
`ErpMealWarehouseService.ImportStatusesAsync` or `ImportPurchasesAsync`.
Use stable ERP record IDs as `SourceRecordKey`. Keep separate feed names, page
through all responses, and run an overlapping lookback window so corrections
to older ERP records update the warehouse. Do not delete existing rows from a
partial or empty API response. The API schema, authentication, paging, and
stable record ID must be confirmed with the ERP vendor before scheduling it.

## SQL import

`backend/sql/generated/erp-meal-import.sql` is the ready-to-run import for all
three supplied exports. It stages the rows in temporary tables, joins the ERP
student ID to `StudentProfiles.SISStudentCode`, and upserts the facts in a
transaction. It also writes three `ErpMealSyncRuns` rows. Apply the consolidated
`AddErpStudentMeals` migration before running it. The file contains student data,
has owner-only permissions, and is ignored by Git.

```bash
mysql -h <rds-endpoint> -P 3306 -u <user> -p --default-character-set=utf8mb4 \
  olympia_student_360 < backend/sql/generated/erp-meal-import.sql
```

Replaying the same SQL file updates existing fact rows and records another sync
run. The API worker detects that run and rebuilds the report aggregates.

## Student meal reports

The `/reports` page contains a **Suất ăn học sinh** group. Its queries read
`ErpMealDailyAggregates`, which stores one row per student, day, and meal and
is indexed for date, student, week, and month lookup. Row drill-downs read
`ErpMealReportDetails`, which is rebuilt in the same transaction and stores
one row per counted meal event or Coffee Shop-coded item line. The detail view
shows student, date, class, meal and ERP order/ticket status, or product code,
name, and quantity for canteen lines. Each successful ERP meal ETL invocation
rebuilds both tables after all selected feeds have committed. The API background
worker is a fallback: it detects a newer ERP sync run, imported report changes,
missing detail rows, or a new UTC day and rebuilds when needed. The ETL and
worker share a MySQL named lock so only one rebuild runs at a time. A build
marker is published in the same transaction as the refreshed rows, so readers
see a complete build. After a direct SQL import, rebuild immediately with:

```bash
dotnet run --project backend/OlympiaStudent360.ErpMealEtl -- --rebuild-aggregates-only
```

The local export snapshot currently yields these distinct measures:

| Report | Measure | Snapshot total |
| --- | --- | ---: |
| Ordered but no ticket | `missing_ticket` rows with `Có đặt ăn` and `Không lấy phiếu ăn`; one per student/day/meal | 2,208 meals, 371 students |
| No meal order | `missing_order` rows with `Không đặt ăn`; one per student/day/meal | 12,612 meals, 429 students |
| Coffee Shop frequency | Canteen lines with product code prefix `CFS_` or `CF_` | 10,514 item lines, 11,033 units, 740 students |

Both status exports contain overlapping student/day/meal records. The two
status reports use their own feed and do not add both feeds together. The
"ordered but did not eat" report is displayed as unavailable: order and
ticket status cannot establish actual consumption. Ask the ERP vendor for a
consumption or meal attendance event before enabling that report. Coffee Shop
is inferred from product codes; confirm this rule with the ERP vendor. The
export has no receipt/transaction ID, sales location, unit price, or amount,
so frequency means item lines and units, never number of purchases or spend.
Likewise, the no-order report covers the provided ERP export, not every student
on dates missing from that export.

The report supports day, month, selected school period, and custom date ranges;
student name or ERP/SIS code search; grade and class filters; and student,
week, month, grade, and class grouping. Student rows link to `StudentProfiles`
only when `SISStudentCode` matched the ERP ID. Unmatched rows remain visible to
full academic users, with source class when available; restricted teacher
accounts only see rows linked to students in their permitted scope.
