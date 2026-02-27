using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibrarySystemBBU.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryLogWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LibraryLogItems_LogId",
                table: "LibraryLogItems");

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedUtc",
                table: "LibraryLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedUtc",
                table: "LibraryLogs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "LibraryLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedDate",
                table: "LibraryLogItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogs_Status",
                table: "LibraryLogs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogs_VisitDate",
                table: "LibraryLogs",
                column: "VisitDate");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogItems_LogId_BookId",
                table: "LibraryLogItems",
                columns: new[] { "LogId", "BookId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LibraryLogs_Status",
                table: "LibraryLogs");

            migrationBuilder.DropIndex(
                name: "IX_LibraryLogs_VisitDate",
                table: "LibraryLogs");

            migrationBuilder.DropIndex(
                name: "IX_LibraryLogItems_LogId_BookId",
                table: "LibraryLogItems");

            migrationBuilder.DropColumn(
                name: "ApprovedUtc",
                table: "LibraryLogs");

            migrationBuilder.DropColumn(
                name: "ReturnedUtc",
                table: "LibraryLogs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LibraryLogs");

            migrationBuilder.DropColumn(
                name: "ReturnedDate",
                table: "LibraryLogItems");

            migrationBuilder.CreateIndex(
                name: "IX_LibraryLogItems_LogId",
                table: "LibraryLogItems",
                column: "LogId");
        }
    }
}
