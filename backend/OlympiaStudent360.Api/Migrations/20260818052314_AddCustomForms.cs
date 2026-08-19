using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomForms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PublicId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    ConfirmationMessage = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AllowMultipleSubmissions = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    OpensAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosesAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponseLimit = table.Column<int>(type: "int", nullable: true),
                    DefinitionJson = table.Column<string>(type: "longtext", nullable: false),
                    HistoryColumnIdsJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    CreatedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomForms", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomFormAccessSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomFormId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFormAccessSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFormAccessSessions_CustomForms_CustomFormId",
                        column: x => x.CustomFormId,
                        principalTable: "CustomForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomFormAccessSessions_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomFormResponses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomFormId = table.Column<int>(type: "int", nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    RespondentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    RespondentRelationship = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    RespondentEmail = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    RespondentPhone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    AnswersJson = table.Column<string>(type: "longtext", nullable: false),
                    QuestionSnapshotJson = table.Column<string>(type: "longtext", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFormResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFormResponses_CustomForms_CustomFormId",
                        column: x => x.CustomFormId,
                        principalTable: "CustomForms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomFormResponses_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormAccessSessions_CustomFormId",
                table: "CustomFormAccessSessions",
                column: "CustomFormId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormAccessSessions_ExpiresAt",
                table: "CustomFormAccessSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormAccessSessions_StudentProfileId",
                table: "CustomFormAccessSessions",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormAccessSessions_TokenHash",
                table: "CustomFormAccessSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormResponses_CustomFormId_IsDeleted_CreatedAt",
                table: "CustomFormResponses",
                columns: new[] { "CustomFormId", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormResponses_CustomFormId_StudentProfileId",
                table: "CustomFormResponses",
                columns: new[] { "CustomFormId", "StudentProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFormResponses_StudentProfileId",
                table: "CustomFormResponses",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomForms_IsArchived_IsActive",
                table: "CustomForms",
                columns: new[] { "IsArchived", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomForms_PublicId",
                table: "CustomForms",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFormAccessSessions");

            migrationBuilder.DropTable(
                name: "CustomFormResponses");

            migrationBuilder.DropTable(
                name: "CustomForms");
        }
    }
}
