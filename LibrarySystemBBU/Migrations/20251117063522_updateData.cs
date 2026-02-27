using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class updateData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoryRecords",
                columns: table => new
                {
                    HistoryId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: true),
                    CatalogId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MemberId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActionUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AdjustmentId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ClientIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DepositAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    FineAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ISBN = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LoanId = table.Column<int>(type: "int", nullable: true),
                    MemberName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: true),
                    PerformedByUserName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PurchaseId = table.Column<int>(type: "int", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    ReturnId = table.Column<int>(type: "int", nullable: true),
                    SourceAction = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceController = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tag1 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Tag2 = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryRecords", x => x.HistoryId);
                    table.ForeignKey(
                        name: "FK_HistoryRecords_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "BookId");
                    table.ForeignKey(
                        name: "FK_HistoryRecords_Catalogs_CatalogId",
                        column: x => x.CatalogId,
                        principalTable: "Catalogs",
                        principalColumn: "CatalogId");
                    table.ForeignKey(
                        name: "FK_HistoryRecords_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "MemberId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_ActionUtc",
                table: "HistoryRecords",
                column: "ActionUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_BookId",
                table: "HistoryRecords",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_CatalogId",
                table: "HistoryRecords",
                column: "CatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_EntityType_ActionType",
                table: "HistoryRecords",
                columns: new[] { "EntityType", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_LoanId",
                table: "HistoryRecords",
                column: "LoanId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryRecords_MemberId",
                table: "HistoryRecords",
                column: "MemberId");
        }
    }
}
