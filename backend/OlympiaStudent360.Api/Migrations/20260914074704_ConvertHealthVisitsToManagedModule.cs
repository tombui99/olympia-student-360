using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConvertHealthVisitsToManagedModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SISHealthVisits",
                table: "SISHealthVisits");

            migrationBuilder.RenameTable(
                name: "SISHealthVisits",
                newName: "StudentHealthVisits");

            migrationBuilder.RenameIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "StudentHealthVisits",
                newName: "IX_StudentHealthVisits_StudentProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_SISHealthVisits_SISUserId",
                table: "StudentHealthVisits",
                newName: "IX_StudentHealthVisits_SISUserId");

            migrationBuilder.RenameIndex(
                name: "IX_SISHealthVisits_ExamTime",
                table: "StudentHealthVisits",
                newName: "IX_StudentHealthVisits_ExamTime");

            migrationBuilder.AddColumn<string>(
                name: "CustomFieldsJson",
                table: "StudentHealthVisits",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ImportHistoryId",
                table: "StudentHealthVisits",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StudentHealthVisits",
                table: "StudentHealthVisits",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHealthVisits_ImportHistoryId",
                table: "StudentHealthVisits",
                column: "ImportHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentHealthVisits_ImportHistories_ImportHistoryId",
                table: "StudentHealthVisits",
                column: "ImportHistoryId",
                principalTable: "ImportHistories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentHealthVisits_StudentProfiles_StudentProfileId",
                table: "StudentHealthVisits",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentHealthVisits_ImportHistories_ImportHistoryId",
                table: "StudentHealthVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_StudentHealthVisits_StudentProfiles_StudentProfileId",
                table: "StudentHealthVisits");

            migrationBuilder.DropPrimaryKey(
                name: "PK_StudentHealthVisits",
                table: "StudentHealthVisits");

            migrationBuilder.DropIndex(
                name: "IX_StudentHealthVisits_ImportHistoryId",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "CustomFieldsJson",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "ImportHistoryId",
                table: "StudentHealthVisits");

            migrationBuilder.RenameTable(
                name: "StudentHealthVisits",
                newName: "SISHealthVisits");

            migrationBuilder.RenameIndex(
                name: "IX_StudentHealthVisits_StudentProfileId",
                table: "SISHealthVisits",
                newName: "IX_SISHealthVisits_StudentProfileId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentHealthVisits_SISUserId",
                table: "SISHealthVisits",
                newName: "IX_SISHealthVisits_SISUserId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentHealthVisits_ExamTime",
                table: "SISHealthVisits",
                newName: "IX_SISHealthVisits_ExamTime");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SISHealthVisits",
                table: "SISHealthVisits",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
