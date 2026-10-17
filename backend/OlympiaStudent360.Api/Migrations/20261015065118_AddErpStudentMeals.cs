using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddErpStudentMeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErpCanteenPurchases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceRecordKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    ErpStudentId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StudentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ClassCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    PurchaseDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProductCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ProductName = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    MatchStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpCanteenPurchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpCanteenPurchases_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpMealStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Feed = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    SourceRecordKey = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    ErpStudentId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StudentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ClassCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    MealDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Meal = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    OrderStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TicketStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    MatchStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMealStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpMealStatuses_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpMealSyncRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Feed = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    SourceDescription = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ReceivedRows = table.Column<int>(type: "int", nullable: false),
                    InsertedRows = table.Column<int>(type: "int", nullable: false),
                    UpdatedRows = table.Column<int>(type: "int", nullable: false),
                    UnmatchedRows = table.Column<int>(type: "int", nullable: false),
                    ConflictRows = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMealSyncRuns", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpMealDailyAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    ErpStudentId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StudentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Grade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EventDay = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MonthStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Meal = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    OrderedNoTicketCount = table.Column<int>(type: "int", nullable: false),
                    NoOrderCount = table.Column<int>(type: "int", nullable: false),
                    CoffeeLineCount = table.Column<int>(type: "int", nullable: false),
                    CoffeeUnitCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMealDailyAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpMealDailyAggregates_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpMealReportBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LastSyncRunId = table.Column<int>(type: "int", nullable: false),
                    LastStudentChangeId = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMealReportBuilds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ErpMealReportDetails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Report = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    StudentKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    ErpStudentId = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StudentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Grade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    ClassName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EventDay = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WeekStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MonthStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Meal = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    OrderStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    TicketStatus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ProductCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ProductName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SourceRowId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErpMealReportDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ErpMealReportDetails_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCanteenPurchases_ErpStudentId",
                table: "ErpCanteenPurchases",
                column: "ErpStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCanteenPurchases_PurchaseDate",
                table: "ErpCanteenPurchases",
                column: "PurchaseDate");

            migrationBuilder.CreateIndex(
                name: "IX_ErpCanteenPurchases_SourceRecordKey",
                table: "ErpCanteenPurchases",
                column: "SourceRecordKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpCanteenPurchases_StudentProfileId",
                table: "ErpCanteenPurchases",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealStatuses_ErpStudentId",
                table: "ErpMealStatuses",
                column: "ErpStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealStatuses_Feed_SourceRecordKey",
                table: "ErpMealStatuses",
                columns: new[] { "Feed", "SourceRecordKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealStatuses_MealDate_Meal",
                table: "ErpMealStatuses",
                columns: new[] { "MealDate", "Meal" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealStatuses_StudentProfileId",
                table: "ErpMealStatuses",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealDailyAggregates_EventDay_Grade_ClassName",
                table: "ErpMealDailyAggregates",
                columns: new[] { "EventDay", "Grade", "ClassName" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealDailyAggregates_MonthStart",
                table: "ErpMealDailyAggregates",
                column: "MonthStart");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealDailyAggregates_StudentKey_EventDay_Meal",
                table: "ErpMealDailyAggregates",
                columns: new[] { "StudentKey", "EventDay", "Meal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealDailyAggregates_StudentProfileId",
                table: "ErpMealDailyAggregates",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealDailyAggregates_WeekStart",
                table: "ErpMealDailyAggregates",
                column: "WeekStart");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealReportDetails_Report_EventDay_StudentKey",
                table: "ErpMealReportDetails",
                columns: new[] { "Report", "EventDay", "StudentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealReportDetails_Report_Grade_ClassName",
                table: "ErpMealReportDetails",
                columns: new[] { "Report", "Grade", "ClassName" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealReportDetails_Report_MonthStart",
                table: "ErpMealReportDetails",
                columns: new[] { "Report", "MonthStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealReportDetails_Report_WeekStart",
                table: "ErpMealReportDetails",
                columns: new[] { "Report", "WeekStart" });

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealReportDetails_StudentProfileId",
                table: "ErpMealReportDetails",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ErpMealSyncRuns_Feed_StartedAtUtc",
                table: "ErpMealSyncRuns",
                columns: new[] { "Feed", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErpMealDailyAggregates");

            migrationBuilder.DropTable(
                name: "ErpMealReportBuilds");

            migrationBuilder.DropTable(
                name: "ErpMealReportDetails");

            migrationBuilder.DropTable(
                name: "ErpCanteenPurchases");

            migrationBuilder.DropTable(
                name: "ErpMealStatuses");

            migrationBuilder.DropTable(
                name: "ErpMealSyncRuns");
        }
    }
}
