using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class Adjustments_AddBookIdDropQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookId",
                table: "AdjustmentDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentDetails_BookId",
                table: "AdjustmentDetails",
                column: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_AdjustmentDetails_Books_BookId",
                table: "AdjustmentDetails",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AdjustmentDetails_Books_BookId",
                table: "AdjustmentDetails");

            migrationBuilder.DropIndex(
                name: "IX_AdjustmentDetails_BookId",
                table: "AdjustmentDetails");

            migrationBuilder.DropColumn(
                name: "BookId",
                table: "AdjustmentDetails");
        }
    }
}
