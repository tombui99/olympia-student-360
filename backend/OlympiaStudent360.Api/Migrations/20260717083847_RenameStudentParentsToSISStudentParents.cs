using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RenameStudentParentsToSISStudentParents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentParents_StudentProfiles_StudentProfileId",
                table: "StudentParents");
            migrationBuilder.DropPrimaryKey(name: "PK_StudentParents", table: "StudentParents");
            migrationBuilder.RenameTable(name: "StudentParents", newName: "SISStudentParents");
            migrationBuilder.RenameIndex(
                name: "IX_StudentParents_StudentProfileId",
                table: "SISStudentParents",
                newName: "IX_SISStudentParents_StudentProfileId");
            migrationBuilder.AddPrimaryKey(name: "PK_SISStudentParents", table: "SISStudentParents", column: "Id");
            migrationBuilder.AddForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents");
            migrationBuilder.DropPrimaryKey(name: "PK_SISStudentParents", table: "SISStudentParents");
            migrationBuilder.RenameTable(name: "SISStudentParents", newName: "StudentParents");
            migrationBuilder.RenameIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "StudentParents",
                newName: "IX_StudentParents_StudentProfileId");
            migrationBuilder.AddPrimaryKey(name: "PK_StudentParents", table: "StudentParents", column: "Id");
            migrationBuilder.AddForeignKey(
                name: "FK_StudentParents_StudentProfiles_StudentProfileId",
                table: "StudentParents",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
