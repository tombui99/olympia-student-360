using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkSisTablesBySisUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.AddColumn<string>(
                name: "SISUserId",
                table: "SISStudentParents",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SISUserId",
                table: "SISHealthVisits",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `SISStudentParents` AS parent
                INNER JOIN `StudentProfiles` AS student ON student.`Id` = parent.`StudentProfileId`
                SET parent.`SISUserId` = student.`SISUserId`;
                """);

            migrationBuilder.Sql("""
                UPDATE `SISHealthVisits` AS visit
                INNER JOIN `StudentProfiles` AS student ON student.`Id` = visit.`StudentProfileId`
                SET visit.`SISUserId` = student.`SISUserId`
                WHERE visit.`StudentProfileId` IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "SISUserId",
                table: "SISStudentParents",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_SISUserId",
                table: "StudentProfiles");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StudentProfiles_SISUserId",
                table: "StudentProfiles",
                column: "SISUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_SISUserId",
                table: "SISStudentParents",
                column: "SISUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SISHealthVisits_SISUserId",
                table: "SISHealthVisits",
                column: "SISUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_SISUserId",
                table: "SISHealthVisits",
                column: "SISUserId",
                principalTable: "StudentProfiles",
                principalColumn: "SISUserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_SISUserId",
                table: "SISStudentParents",
                column: "SISUserId",
                principalTable: "StudentProfiles",
                principalColumn: "SISUserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_SISUserId",
                table: "SISHealthVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_SISUserId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISStudentParents_SISUserId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISHealthVisits_SISUserId",
                table: "SISHealthVisits");

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId",
                table: "SISStudentParents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId",
                table: "SISHealthVisits",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `SISStudentParents` AS parent
                INNER JOIN `StudentProfiles` AS student ON student.`SISUserId` = parent.`SISUserId`
                SET parent.`StudentProfileId` = student.`Id`;
                """);

            migrationBuilder.Sql("""
                UPDATE `SISHealthVisits` AS visit
                INNER JOIN `StudentProfiles` AS student ON student.`SISUserId` = visit.`SISUserId`
                SET visit.`StudentProfileId` = student.`Id`
                WHERE visit.`SISUserId` IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "SISStudentParents",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "SISUserId",
                table: "SISStudentParents");

            migrationBuilder.DropColumn(
                name: "SISUserId",
                table: "SISHealthVisits");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StudentProfiles_SISUserId",
                table: "StudentProfiles");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_SISUserId",
                table: "StudentProfiles",
                column: "SISUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
