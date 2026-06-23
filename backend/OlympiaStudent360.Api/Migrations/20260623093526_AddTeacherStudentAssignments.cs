using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherStudentAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeacherUserId",
                table: "StudentProfiles",
                type: "varchar(255)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_TeacherUserId",
                table: "StudentProfiles",
                column: "TeacherUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudentProfiles_AspNetUsers_TeacherUserId",
                table: "StudentProfiles",
                column: "TeacherUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentProfiles_AspNetUsers_TeacherUserId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_TeacherUserId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "TeacherUserId",
                table: "StudentProfiles");
        }
    }
}
