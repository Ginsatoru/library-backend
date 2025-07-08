using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookLoans_Books_BookId",
                table: "BookLoans");

            migrationBuilder.DropForeignKey(
                name: "FK_Catalogs_Books_BookId1",
                table: "Catalogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Purchases_Books_BookId1",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Purchases_BookId1",
                table: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Catalogs_BookId1",
                table: "Catalogs");

            migrationBuilder.DropColumn(
                name: "BookId1",
                table: "Purchases");

            migrationBuilder.DropColumn(
                name: "BookId1",
                table: "Catalogs");

            migrationBuilder.AlterColumn<decimal>(
                name: "DepositAmount",
                table: "BookLoans",
                type: "decimal(10,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "BookLoans",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<decimal>(
                name: "BorrowingFee",
                table: "BookLoans",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "BookLoans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdjustmentDetails",
                columns: table => new
                {
                    AdjustmentDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantityChanged = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdjustmentDetails", x => x.AdjustmentDetailId);
                    table.ForeignKey(
                        name: "FK_AdjustmentDetails_Adjustments_AdjustmentId",
                        column: x => x.AdjustmentId,
                        principalTable: "Adjustments",
                        principalColumn: "AdjustmentId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdjustmentDetails_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "CatalogId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanBookDetails",
                columns: table => new
                {
                    LoanBookDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LoanId = table.Column<int>(type: "int", nullable: false),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    ConditionOut = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConditionIn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FineDetailAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    FineDetailReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanBookDetails", x => x.LoanBookDetailId);
                    table.ForeignKey(
                        name: "FK_LoanBookDetails_BookLoans_LoanId",
                        column: x => x.LoanId,
                        principalTable: "BookLoans",
                        principalColumn: "LoanId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoanBookDetails_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanBookDetails_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "CatalogId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseDetails",
                columns: table => new
                {
                    PurchaseDetailId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PurchaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Modified = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseDetails", x => x.PurchaseDetailId);
                    table.ForeignKey(
                        name: "FK_PurchaseDetails_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PurchaseDetails_Purchases_PurchaseId",
                        column: x => x.PurchaseId,
                        principalTable: "Purchases",
                        principalColumn: "PurchaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentDetails_AdjustmentId",
                table: "AdjustmentDetails",
                column: "AdjustmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AdjustmentDetails_CatalogId",
                table: "AdjustmentDetails",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanBookDetails_BookId",
                table: "LoanBookDetails",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanBookDetails_CatalogId",
                table: "LoanBookDetails",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_LoanBookDetails_LoanId",
                table: "LoanBookDetails",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseDetails_BookId",
                table: "PurchaseDetails",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseDetails_PurchaseId",
                table: "PurchaseDetails",
                column: "PurchaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookLoans_Books_BookId",
                table: "BookLoans",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookLoans_Books_BookId",
                table: "BookLoans");

            migrationBuilder.DropTable(
                name: "AdjustmentDetails");

            migrationBuilder.DropTable(
                name: "LoanBookDetails");

            migrationBuilder.DropTable(
                name: "PurchaseDetails");

            migrationBuilder.DropColumn(
                name: "BorrowingFee",
                table: "BookLoans");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "BookLoans");

            migrationBuilder.AddColumn<int>(
                name: "BookId1",
                table: "Purchases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookId1",
                table: "Catalogs",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "DepositAmount",
                table: "BookLoans",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BookId",
                table: "BookLoans",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_BookId1",
                table: "Purchases",
                column: "BookId1");

            migrationBuilder.CreateIndex(
                name: "IX_Catalogs_BookId1",
                table: "Catalogs",
                column: "BookId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BookLoans_Books_BookId",
                table: "BookLoans",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "BookId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Catalogs_Books_BookId1",
                table: "Catalogs",
                column: "BookId1",
                principalTable: "Books",
                principalColumn: "BookId");

            migrationBuilder.AddForeignKey(
                name: "FK_Purchases_Books_BookId1",
                table: "Purchases",
                column: "BookId1",
                principalTable: "Books",
                principalColumn: "BookId");
        }
    }
}
