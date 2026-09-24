using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentSupportActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentSupportActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    ActionDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FollowUpDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Category = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Goal = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    ActionTaken = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    Result = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    NextStep = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Progress = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ShareWithFamily = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    UpdatedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentSupportActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentSupportActions_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSupportActions_AcademicYear_Category_Status",
                table: "StudentSupportActions",
                columns: new[] { "AcademicYear", "Category", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentSupportActions_ActionDate",
                table: "StudentSupportActions",
                column: "ActionDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentSupportActions_StudentProfileId",
                table: "StudentSupportActions",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentSupportActions");
        }
    }
}
