using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibrarySystemBBU.Services;
using LibrarySystemBBU.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LibrarySystemBBU.Controllers
{
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        public async Task<IActionResult> Monthly(DateTime? startDate, DateTime? endDate)
        {
            var start = startDate?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var end = endDate?.Date ?? DateTime.Today;

            if (end < start) end = start;

            var vm = await _reportService.GetReportAsync(start, end);
            return View(vm);
        }

        public async Task<IActionResult> MonthlyExcel(DateTime startDate, DateTime endDate)
        {
            var vm = await _reportService.GetReportAsync(startDate.Date, endDate.Date);

            var html = BuildExcelHtml(vm);
            var bytes = AddUtf8Bom(html);

            var fileName = $"Report_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.xls";
            const string contentType = "application/vnd.ms-excel; charset=utf-8";
            return File(bytes, contentType, fileName);
        }

        private static string HtmlEncode(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private static string BuildExcelHtml(MonthlyReportViewModel vm)
        {
            var sb = new StringBuilder();
            var rangeLabel = $"{vm.StartDate:yyyy-MM-dd} to {vm.EndDate:yyyy-MM-dd}";

            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");

            // 1) Borrowed
            sb.AppendLine($"<h3>Borrow Book Report - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Borrow Date</th><th>Due</th><th>Return</th>" +
                "<th>Member</th><th>Type</th><th>Catalog Title</th><th>Barcode</th>" +
                "<th>Deposit ($)</th><th>Fine Amount ($)</th><th>Extra Charge ($)</th>" +
                "</tr>");

            decimal totalDeposit = 0, totalFine = 0, totalExtra = 0;
            foreach (var b in vm.StudentBorrowDetails)
            {
                totalDeposit += b.DepositAmount;
                totalFine += b.FineAmount;
                totalExtra += b.ExtraCharge;
                sb.Append("<tr>");
                sb.Append($"<td>{b.LoanDate:yyyy-MM-dd}</td>");
                sb.Append($"<td>{b.DueDate?.ToString("yyyy-MM-dd")}</td>");
                sb.Append($"<td>{b.ReturnDate?.ToString("yyyy-MM-dd")}</td>");
                sb.Append($"<td>{HtmlEncode(b.MemberName)}</td>");
                sb.Append($"<td>{HtmlEncode(b.MemberType)}</td>");
                sb.Append($"<td>{HtmlEncode(b.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(b.Barcode)}</td>");
                sb.Append($"<td>${b.DepositAmount:0}</td>");
                sb.Append($"<td>${b.FineAmount:0}</td>");
                sb.Append($"<td>${b.ExtraCharge:0}</td>");
                sb.Append("</tr>");
            }
            var borrowStudents = vm.StudentBorrowDetails.Select(x => x.MemberId).Distinct().Count();
            sb.Append($"<tr><td colspan='3'><b>Totals</b></td><td><b>{borrowStudents} student(s)</b></td><td></td>");
            sb.Append($"<td><b>{vm.StudentBorrowDetails.Count} item(s)</b></td><td></td>");
            sb.Append($"<td><b>${totalDeposit:0}</b></td><td><b>${totalFine:0}</b></td><td><b>${totalExtra:0}</b></td></tr>");
            sb.AppendLine("</table><br />");

            // 1b) Returned
            sb.AppendLine($"<h3>Returned Books Report - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Return Date</th><th>Borrow Date</th><th>Due</th>" +
                "<th>Member</th><th>Type</th><th>Catalog Title</th><th>Barcode</th>" +
                "<th>Fine Amount ($)</th><th>Extra Charge ($)</th>" +
                "</tr>");

            decimal retFine = 0, retExtra = 0;
            foreach (var r in vm.StudentReturnDetails)
            {
                retFine += r.FineAmount;
                retExtra += r.ExtraCharge;
                sb.Append("<tr>");
                sb.Append($"<td>{r.ReturnDate?.ToString("yyyy-MM-dd")}</td>");
                sb.Append($"<td>{r.LoanDate:yyyy-MM-dd}</td>");
                sb.Append($"<td>{r.DueDate?.ToString("yyyy-MM-dd")}</td>");
                sb.Append($"<td>{HtmlEncode(r.MemberName)}</td>");
                sb.Append($"<td>{HtmlEncode(r.MemberType)}</td>");
                sb.Append($"<td>{HtmlEncode(r.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Barcode)}</td>");
                sb.Append($"<td>${r.FineAmount:0}</td>");
                sb.Append($"<td>${r.ExtraCharge:0}</td>");
                sb.Append("</tr>");
            }
            var retStudents = vm.StudentReturnDetails.Select(x => x.MemberId).Distinct().Count();
            sb.Append($"<tr><td colspan='3'><b>Totals</b></td><td><b>{retStudents} student(s)</b></td><td></td>");
            sb.Append($"<td><b>{vm.StudentReturnDetails.Count} item(s)</b></td><td></td>");
            sb.Append($"<td><b>${retFine:0}</b></td><td><b>${retExtra:0}</b></td></tr>");
            sb.AppendLine("</table><br />");

            // 2) In-Library
            sb.AppendLine($"<h3>In-Library Reading - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Visit Date</th><th>Student</th><th>Gender</th><th>Purpose</th>" +
                          "<th>Book Title</th><th>Catalog Title</th><th>Barcode</th><th>Status</th><th>Returned Date</th></tr>");
            foreach (var r in vm.InLibraryDetails)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{r.VisitDate:yyyy-MM-dd}</td>");
                sb.Append($"<td>{HtmlEncode(r.StudentName)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Gender)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Purpose)}</td>");
                sb.Append($"<td>{HtmlEncode(r.BookTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Barcode)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Status)}</td>");
                sb.Append($"<td>{r.ReturnedDate?.ToString("yyyy-MM-dd")}</td>");
                sb.Append("</tr>");
            }
            var inLibStudents = vm.InLibraryDetails.Select(x => x.StudentName).Distinct().Count();
            sb.Append($"<tr><td colspan='2'><b>{inLibStudents} unique student(s)</b></td>");
            sb.Append($"<td colspan='7'><b>{vm.InLibraryDetails.Count} item(s) read in library</b></td></tr>");
            sb.AppendLine("</table><br />");

            // 3) Purchases
            sb.AppendLine($"<h3>Purchases - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Date</th><th>Supplier</th><th>Book Title</th><th>Quantity</th><th>Cost ($)</th><th>Notes</th></tr>");
            int totalQty = 0; decimal totalCost = 0;
            foreach (var p in vm.PurchaseItems)
            {
                totalQty += p.Quantity; totalCost += p.Cost;
                sb.Append("<tr>");
                sb.Append($"<td>{p.PurchaseDate:yyyy-MM-dd}</td>");
                sb.Append($"<td>{HtmlEncode(p.Supplier)}</td>");
                sb.Append($"<td>{HtmlEncode(p.BookTitle)}</td>");
                sb.Append($"<td>{p.Quantity}</td>");
                sb.Append($"<td>${p.Cost:0}</td>");
                sb.Append($"<td>{HtmlEncode(p.Notes)}</td>");
                sb.Append("</tr>");
            }
            sb.Append($"<tr><td colspan='3'><b>{vm.PurchaseItems.Count} row(s)</b></td>");
            sb.Append($"<td><b>{totalQty}</b></td><td><b>${totalCost:0}</b></td><td></td></tr>");
            sb.AppendLine("</table><br />");

            // 4) Adjustments
            sb.AppendLine($"<h3>Adjustments - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Date</th><th>Type</th><th>Catalog Title</th><th>Qty Change</th><th>Reason</th></tr>");
            int totalAdjQty = 0;
            foreach (var d in vm.AdjustmentItems)
            {
                totalAdjQty += d.QuantityChanged;
                sb.Append("<tr>");
                sb.Append($"<td>{d.AdjustmentDate:yyyy-MM-dd}</td>");
                sb.Append($"<td>{HtmlEncode(d.AdjustmentType)}</td>");
                sb.Append($"<td>{HtmlEncode(d.CatalogTitle)}</td>");
                sb.Append($"<td>{d.QuantityChanged}</td>");
                sb.Append($"<td>{HtmlEncode(d.Reason)}</td>");
                sb.Append("</tr>");
            }
            sb.Append($"<tr><td colspan='3'><b>{vm.AdjustmentItems.Count} row(s)</b></td>");
            sb.Append($"<td><b>{totalAdjQty}</b></td><td></td></tr>");
            sb.AppendLine("</table><br />");

            // 5) Financial Summary
            sb.AppendLine($"<h3>Financial Summary - {HtmlEncode(rangeLabel)}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Category</th><th>Type</th><th>Amount ($)</th></tr>");
            sb.Append($"<tr><td>Borrowing fees</td><td>Income</td><td>${vm.IncomeBorrowingFees:0}</td></tr>");
            sb.Append($"<tr><td>Late fines</td><td>Income</td><td>${vm.IncomeFines:0}</td></tr>");
            sb.Append($"<tr><td>Extra charges</td><td>Income</td><td>${vm.IncomeExtraCharges:0}</td></tr>");
            sb.Append($"<tr><td>Book purchases</td><td>Expense</td><td>${vm.ExpensePurchases:0}</td></tr>");
            sb.Append($"<tr><td colspan='2'><b>Net Cash Flow</b></td><td><b>${vm.NetCashFlow:0}</b></td></tr>");
            sb.AppendLine("</table>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static byte[] AddUtf8Bom(string s)
        {
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8Bom.GetBytes(s);
        }
    }
}