using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentTranscriptRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentTranscriptRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    RecordJson = table.Column<string>(type: "longtext", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    UpdatedBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentTranscriptRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentTranscriptRecords_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentTranscriptRecords_StudentProfileId",
                table: "StudentTranscriptRecords",
                column: "StudentProfileId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentTranscriptRecords");
        }
    }
}
