using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileHashIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Files_FileHash",
                table: "Files",
                column: "FileHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Files_FileHash",
                table: "Files");
        }
    }
}
