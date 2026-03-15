using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class AddLibrarySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookBorrows_Books_BookId",
                table: "BookBorrows");

            migrationBuilder.DropIndex(
                name: "IX_BookBorrows_BookId",
                table: "BookBorrows");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "BookBorrows");

            migrationBuilder.CreateTable(
                name: "LibrarySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    WeekdayHours = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WeekendHours = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LibrarySettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LibrarySettings");

            migrationBuilder.AddColumn<int>(
                name: "BookId",
                table: "BookBorrows",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookBorrows_BookId",
                table: "BookBorrows",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookBorrows_Books_BookId",
                table: "BookBorrows",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId");
        }
    }
}
