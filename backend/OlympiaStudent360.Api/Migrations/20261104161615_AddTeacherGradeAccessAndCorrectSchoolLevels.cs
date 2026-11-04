using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherGradeAccessAndCorrectSchoolLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeacherGradeAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    GradeLevel = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherGradeAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeacherGradeAccesses_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherGradeAccesses_UserId_GradeLevel",
                table: "TeacherGradeAccesses",
                columns: new[] { "UserId", "GradeLevel" },
                unique: true);

            // SIS grade is authoritative, with Canvas only a fallback for unclassified profiles.
            // Correct existing Grade 9 rows before publishing their report invalidations.
            migrationBuilder.Sql("""
                UPDATE `StudentProfiles` SET `EducationLevel` = 'UpperSecondary'
                WHERE COALESCE(NULLIF(TRIM(`GradeLevel`), ''), TRIM(`CanvasGradeLevel`)) = '9';
                INSERT INTO `ImportedReportChanges` (`Areas`, `StudentProfileId`, `CreatedAt`)
                SELECT 31, `Id`, UTC_TIMESTAMP(6) FROM `StudentProfiles`
                WHERE COALESCE(NULLIF(TRIM(`GradeLevel`), ''), TRIM(`CanvasGradeLevel`)) = '9';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeacherGradeAccesses");
        }
    }
}
