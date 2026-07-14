using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentHealthIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentHealthIncidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    IncidentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    IncidentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SeverityLevel = table.Column<int>(type: "int", nullable: false),
                    BodyArea = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Location = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Cause = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    ActionTaken = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    RecordedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentHealthIncidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentHealthIncidents_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHealthIncidents_AcademicYear_SeverityLevel",
                table: "StudentHealthIncidents",
                columns: new[] { "AcademicYear", "SeverityLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentHealthIncidents_IncidentDate",
                table: "StudentHealthIncidents",
                column: "IncidentDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHealthIncidents_StudentProfileId",
                table: "StudentHealthIncidents",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentHealthIncidents");
        }
    }
}
