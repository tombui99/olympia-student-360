using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class IndexSisTranscriptStudent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SisWarehouse_TranscriptStudent",
                table: "SisWarehouseRecords",
                columns: new[] { "Endpoint", "StudentUserId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SisWarehouse_TranscriptStudent",
                table: "SisWarehouseRecords");
        }
    }
}
