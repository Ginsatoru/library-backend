using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class Add_LibraryLogItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LibraryLogs_Books_BookId",
                table: "LibraryLogs");

            migrationBuilder.DropIndex(
                name: "IX_LibraryLogs_BookId",
                table: "LibraryLogs");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "LibraryLogs");

            migrationBuilder.CreateTable(
                name: "LibraryLogItems",
                columns: table => new
                {
                    LogItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LogId = table.Column<int>(type: "int", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibraryLogItems", x => x.LogItemId);
                    table.ForeignKey(
                        name: "FK_LibraryLogItems_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LibraryLogItems_LibraryLogs_LogId",
                        column: x => x.LogId,
                        principalTable: "LibraryLogs",
                        principalColumn: "LogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogItems_BookId",
                table: "LibraryLogItems",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogItems_LogId",
                table: "LibraryLogItems",
                column: "LogId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibraryLogItems");

            migrationBuilder.AddColumn<int>(
                name: "BookId",
                table: "LibraryLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogs_BookId",
                table: "LibraryLogs",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_LibraryLogs_Books_BookId",
                table: "LibraryLogs",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
