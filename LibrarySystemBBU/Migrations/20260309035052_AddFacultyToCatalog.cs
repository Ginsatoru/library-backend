using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class AddFacultyToCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Faculty",
                table: "Catalogs",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Faculty",
                table: "Catalogs");
        }
    }
}
