using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TextAnalyzer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Files_Sessions_SessionId",
                table: "Files");

            migrationBuilder.DropForeignKey(
                name: "FK_Results_Sessions_SessionId",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_Results_SessionId",
                table: "Results");

            migrationBuilder.DropIndex(
                name: "IX_Files_SessionId",
                table: "Files");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Files");

            migrationBuilder.CreateTable(
                name: "FileEntitySessionEntity",
                columns: table => new
                {
                    FilesId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileEntitySessionEntity", x => new { x.FilesId, x.SessionsId });
                    table.ForeignKey(
                        name: "FK_FileEntitySessionEntity_Files_FilesId",
                        column: x => x.FilesId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FileEntitySessionEntity_Sessions_SessionsId",
                        column: x => x.SessionsId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileEntitySessionEntity_SessionsId",
                table: "FileEntitySessionEntity",
                column: "SessionsId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileEntitySessionEntity");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Results",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "Files",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Results_SessionId",
                table: "Results",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Files_SessionId",
                table: "Files",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Files_Sessions_SessionId",
                table: "Files",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Results_Sessions_SessionId",
                table: "Results",
                column: "SessionId",
                principalTable: "Sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
