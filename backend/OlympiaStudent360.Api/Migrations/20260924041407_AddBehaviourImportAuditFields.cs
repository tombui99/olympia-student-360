using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBehaviourImportAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentBehaviours_StudentProfiles_StudentProfileId",
                table: "StudentBehaviours");

            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "StudentBehaviours",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "StudentBehaviours",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BehaviourType",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "BehaviourPointsDeducted",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConductDeductionLevel",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Decision",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncidentTimeText",
                table: "StudentBehaviours",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccurrenceCount",
                table: "StudentBehaviours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByEmail",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByTitle",
                table: "StudentBehaviours",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDataIssues",
                table: "StudentBehaviours",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceGradeLevel",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRecordId",
                table: "StudentBehaviours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRowNumber",
                table: "StudentBehaviours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceStudentName",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMatchNotes",
                table: "StudentBehaviours",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMatchStatus",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRecordId",
                table: "StudentBehaviours",
                columns: new[] { "SourceFileName", "SourceRecordId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBehaviours_StudentProfiles_StudentProfileId",
                table: "StudentBehaviours",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StudentBehaviours_StudentProfiles_StudentProfileId",
                table: "StudentBehaviours");

            migrationBuilder.DropIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRecordId",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "BehaviourPointsDeducted",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "ConductDeductionLevel",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "IncidentTimeText",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "OccurrenceCount",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "RecordedByEmail",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "RecordedByTitle",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceDataIssues",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceGradeLevel",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceRowNumber",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceStudentName",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "StudentMatchNotes",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "StudentMatchStatus",
                table: "StudentBehaviours");

            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "StudentBehaviours",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Notes",
                table: "StudentBehaviours",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BehaviourType",
                table: "StudentBehaviours",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddForeignKey(
                name: "FK_StudentBehaviours_StudentProfiles_StudentProfileId",
                table: "StudentBehaviours",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
