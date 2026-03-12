using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseDetailCatalogAndBarcode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "Purchases",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "PurchaseDetails",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "PurchaseDetails",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CatalogId",
                table: "PurchaseDetails",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseDetails_CatalogId",
                table: "PurchaseDetails",
                column: "CatalogId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseDetails_Catalogs_CatalogId",
                table: "PurchaseDetails",
                column: "CatalogId",
                principalTable: "Catalogs",
                principalColumn: "CatalogId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseDetails_Catalogs_CatalogId",
                table: "PurchaseDetails");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseDetails_CatalogId",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "CatalogId",
                table: "PurchaseDetails");

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "Purchases",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "PurchaseDetails",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
