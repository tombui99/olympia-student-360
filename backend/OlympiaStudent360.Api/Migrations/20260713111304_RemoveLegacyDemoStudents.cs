using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyDemoStudents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM `StudentProfiles`
                WHERE `TeacherUserId` IS NULL AND (
                    (`StudentCode` = 'S1001' AND `FullName` = 'Nguyễn Minh Anh') OR
                    (`StudentCode` = 'S1002' AND `FullName` = 'Trần Gia Bảo') OR
                    (`StudentCode` = 'S1003' AND `FullName` = 'Lê Khánh Chi') OR
                    (`StudentCode` = 'S1004' AND `FullName` = 'Phạm Đức Duy') OR
                    (`StudentCode` = 'S1005' AND `FullName` = 'Hoàng Thu Hà') OR
                    (`StudentCode` = 'S1006' AND `FullName` = 'Vũ Nam Khánh') OR
                    (`StudentCode` = 'S1007' AND `FullName` = 'Đặng Bảo Linh') OR
                    (`StudentCode` = 'S1008' AND `FullName` = 'Bùi Quang Minh')
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Removed seed rows are intentionally not recreated on rollback.
        }
    }
}
