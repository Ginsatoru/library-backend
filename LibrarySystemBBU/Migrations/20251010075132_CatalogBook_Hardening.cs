using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class CatalogBook_Hardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanBookDetails_Books_BookId",
                table: "LoanBookDetails");

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Catalogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Catalogs",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            //migrationBuilder.AddCheckConstraint(
            //    name: "CK_Catalog_Available_LE_Total",
            //    table: "Catalogs",
            //    sql: "[AvailableCopies] <= [TotalCopies]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Catalog_AvailableCopies_NonNegative",
                table: "Catalogs",
                sql: "[AvailableCopies] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Catalog_TotalCopies_NonNegative",
                table: "Catalogs",
                sql: "[TotalCopies] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Barcode",
                table: "Books",
                column: "Barcode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanBookDetails_Books_BookId",
                table: "LoanBookDetails",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanBookDetails_Books_BookId",
                table: "LoanBookDetails");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Catalog_Available_LE_Total",
                table: "Catalogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Catalog_AvailableCopies_NonNegative",
                table: "Catalogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Catalog_TotalCopies_NonNegative",
                table: "Catalogs");

            migrationBuilder.DropIndex(
                name: "IX_Books_Barcode",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Catalogs");

            migrationBuilder.AlterColumn<string>(
                name: "ISBN",
                table: "Catalogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LoanBookDetails_Books_BookId",
                table: "LoanBookDetails",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
