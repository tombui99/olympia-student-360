using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSchoolOnlineSisWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SISIsActive",
                table: "StudentProfiles",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SISIsDeleted",
                table: "StudentProfiles",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SchoolOnlineSchoolId",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<string>(
                name: "SchoolOnlineSchoolYearId",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<string>(
                name: "SchoolOnlineUserId",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<bool>(
                name: "SISIsDeleted",
                table: "SISStudentParents",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SisSyncRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    Mode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Records = table.Column<int>(type: "int", nullable: false),
                    Scans = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SisSyncRuns", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SisSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    Endpoint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    FilterKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    FiltersJson = table.Column<string>(type: "json", nullable: false),
                    LastSuccessfulWatermark = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    LastCompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SisSyncStates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SisWarehouseRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    Endpoint = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_bin"),
                    RecordKey = table.Column<string>(type: "varchar(191)", maxLength: 191, nullable: false, collation: "utf8mb4_bin"),
                    SourceId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_bin"),
                    SchoolId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_bin"),
                    SchoolYearId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_bin"),
                    StudentUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true, collation: "utf8mb4_bin"),
                    RecordType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    RecordDate = table.Column<DateTime>(type: "date", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PayloadJson = table.Column<string>(type: "json", nullable: false),
                    LastSeenScanId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SisWarehouseRecords", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_SchoolOnlineUserId",
                table: "StudentProfiles",
                column: "SchoolOnlineUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SisSyncRuns_SourceKey_StartedAtUtc",
                table: "SisSyncRuns",
                columns: new[] { "SourceKey", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SisSyncStates_SourceKey_Endpoint_FilterKey",
                table: "SisSyncStates",
                columns: new[] { "SourceKey", "Endpoint", "FilterKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SisWarehouseRecords_SourceKey_Endpoint_RecordKey",
                table: "SisWarehouseRecords",
                columns: new[] { "SourceKey", "Endpoint", "RecordKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SisWarehouseRecords_SourceKey_Endpoint_SchoolYearId_RecordDa~",
                table: "SisWarehouseRecords",
                columns: new[] { "SourceKey", "Endpoint", "SchoolYearId", "RecordDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SisWarehouseRecords_SourceKey_StudentUserId",
                table: "SisWarehouseRecords",
                columns: new[] { "SourceKey", "StudentUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SisSyncRuns");

            migrationBuilder.DropTable(
                name: "SisSyncStates");

            migrationBuilder.DropTable(
                name: "SisWarehouseRecords");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_SchoolOnlineUserId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISIsActive",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISIsDeleted",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolOnlineSchoolId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolOnlineSchoolYearId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SchoolOnlineUserId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISIsDeleted",
                table: "SISStudentParents");
        }
    }
}
