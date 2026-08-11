using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSisClassAndCohortFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SISClassStudents");

            migrationBuilder.DropTable(
                name: "SISClassTeachers");

            migrationBuilder.DropTable(
                name: "SISClasses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SISClasses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ClassName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SISClassCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClasses", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISClassStudents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SISClassId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClassStudents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISClassStudents_SISClasses_SISClassId",
                        column: x => x.SISClassId,
                        principalTable: "SISClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SISClassStudents_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SISClassTeachers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SISClassId = table.Column<int>(type: "int", nullable: false),
                    TeacherUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    AssignmentRole = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SISClassTeachers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SISClassTeachers_AspNetUsers_TeacherUserId",
                        column: x => x.TeacherUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SISClassTeachers_SISClasses_SISClassId",
                        column: x => x.SISClassId,
                        principalTable: "SISClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_SISClasses_AcademicYear_IsActive",
                table: "SISClasses",
                columns: new[] { "AcademicYear", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SISClasses_SISClassCode",
                table: "SISClasses",
                column: "SISClassCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassStudents_SISClassId_StudentProfileId",
                table: "SISClassStudents",
                columns: new[] { "SISClassId", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassStudents_StudentProfileId",
                table: "SISClassStudents",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SISClassTeachers_SISClassId_TeacherUserId",
                table: "SISClassTeachers",
                columns: new[] { "SISClassId", "TeacherUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISClassTeachers_TeacherUserId",
                table: "SISClassTeachers",
                column: "TeacherUserId");
        }
    }
}
