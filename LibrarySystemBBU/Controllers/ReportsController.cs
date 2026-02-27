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

        // ================== MONTHLY HTML VIEW ==================
        public async Task<IActionResult> Monthly(int? year, int? month)
        {
            var y = year ?? DateTime.Today.Year;
            var m = month ?? DateTime.Today.Month;

            var vm = await _reportService.GetMonthlyReportAsync(y, m);
            return View(vm);   // Views/Reports/Monthly.cshtml
        }

        // ================== MONTHLY EXCEL EXPORT (.xls) ==================
        public async Task<IActionResult> MonthlyExcel(int year, int month)
        {
            var vm = await _reportService.GetMonthlyReportAsync(year, month);

            var html = BuildExcelHtml(vm);
            var bytes = AddUtf8Bom(html);

            var fileName = $"MonthlyReport_{year}_{month:00}.xls";
            const string contentType = "application/vnd.ms-excel; charset=utf-8";
            return File(bytes, contentType, fileName);
        }

        private static string HtmlEncode(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private static string BuildExcelHtml(MonthlyReportViewModel vm)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");

            // ------------------------------------------------------------------
            // 1) Borrowed this month (LoanDate in month)
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>Borrow Book (This Month) - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Loan Date</th>" +
                "<th>Due</th>" +
                "<th>Return</th>" +
                "<th>Member</th>" +
                "<th>Type</th>" +
                "<th>Catalog Title</th>" +
                "<th>Barcode</th>" +
                "<th>Deposit (៛)</th>" +
                "<th>Fine Amount (៛)</th>" +
                "<th>Extra Charge (៛)</th>" +
                "</tr>"
            );

            decimal totalDeposit = 0m;
            decimal totalFine = 0m;
            decimal totalExtra = 0m;

            foreach (var b in vm.StudentBorrowDetails)
            {
                totalDeposit += b.DepositAmount;
                totalFine += b.FineAmount;
                totalExtra += b.ExtraCharge;

                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(b.LoanDate.ToString("yyyy-MM-dd"))}</td>");
                sb.Append($"<td>{HtmlEncode(b.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty)}</td>");
                sb.Append($"<td>{HtmlEncode(b.ReturnDate?.ToString("yyyy-MM-dd") ?? string.Empty)}</td>");
                sb.Append($"<td>{HtmlEncode(b.MemberName)}</td>");
                sb.Append($"<td>{HtmlEncode(b.MemberType)}</td>");
                sb.Append($"<td>{HtmlEncode(b.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(b.Barcode)}</td>");
                sb.Append($"<td>{b.DepositAmount:0}</td>");
                sb.Append($"<td>{b.FineAmount:0}</td>");
                sb.Append($"<td>{b.ExtraCharge:0}</td>");
                sb.Append("</tr>");
            }

            var totalStudents = vm.StudentBorrowDetails
                .Select(x => x.MemberId)
                .Distinct()
                .Count();

            sb.Append("<tr>");
            sb.Append("<td colspan='3'><b>Totals</b></td>");
            sb.Append($"<td><b>{totalStudents} student(s)</b></td>");
            sb.Append("<td></td>");
            sb.Append($"<td><b>{vm.StudentBorrowDetails.Count} item(s)</b></td>");
            sb.Append("<td></td>");
            sb.Append($"<td><b>{totalDeposit:0}</b></td>");
            sb.Append($"<td><b>{totalFine:0}</b></td>");
            sb.Append($"<td><b>{totalExtra:0}</b></td>");
            sb.Append("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            // ------------------------------------------------------------------
            // 1b) Returned this month (ReturnDate in month)  ✅ NEW
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>Returned Books (This Month) - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Return Date</th>" +
                "<th>Loan Date</th>" +
                "<th>Due</th>" +
                "<th>Member</th>" +
                "<th>Type</th>" +
                "<th>Catalog Title</th>" +
                "<th>Barcode</th>" +
                "<th>Fine Amount (៛)</th>" +
                "<th>Extra Charge (៛)</th>" +
                "</tr>"
            );

            decimal returnTotalFine = 0m;
            decimal returnTotalExtra = 0m;

            foreach (var r in vm.StudentReturnDetails)
            {
                returnTotalFine += r.FineAmount;
                returnTotalExtra += r.ExtraCharge;

                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(r.ReturnDate?.ToString("yyyy-MM-dd") ?? string.Empty)}</td>");
                sb.Append($"<td>{HtmlEncode(r.LoanDate.ToString("yyyy-MM-dd"))}</td>");
                sb.Append($"<td>{HtmlEncode(r.DueDate?.ToString("yyyy-MM-dd") ?? string.Empty)}</td>");
                sb.Append($"<td>{HtmlEncode(r.MemberName)}</td>");
                sb.Append($"<td>{HtmlEncode(r.MemberType)}</td>");
                sb.Append($"<td>{HtmlEncode(r.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Barcode)}</td>");
                sb.Append($"<td>{r.FineAmount:0}</td>");
                sb.Append($"<td>{r.ExtraCharge:0}</td>");
                sb.Append("</tr>");
            }

            var returnedStudents = vm.StudentReturnDetails
                .Select(x => x.MemberId)
                .Distinct()
                .Count();

            sb.Append("<tr>");
            sb.Append("<td colspan='3'><b>Totals</b></td>");
            sb.Append($"<td><b>{returnedStudents} student(s)</b></td>");
            sb.Append("<td></td>");
            sb.Append($"<td><b>{vm.StudentReturnDetails.Count} item(s) returned</b></td>");
            sb.Append("<td></td>");
            sb.Append($"<td><b>{returnTotalFine:0}</b></td>");
            sb.Append($"<td><b>{returnTotalExtra:0}</b></td>");
            sb.Append("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            // ------------------------------------------------------------------
            // 2) In-Library Reading
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>In-Library Reading - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Visit Date</th>" +
                "<th>Student</th>" +
                "<th>Gender</th>" +
                "<th>Purpose</th>" +
                "<th>Book Title</th>" +
                "<th>Catalog Title</th>" +
                "<th>Barcode</th>" +
                "<th>Status</th>" +
                "<th>Returned Date</th>" +
                "</tr>"
            );

            var inLibStudents = vm.InLibraryDetails
                .Select(x => x.StudentName)
                .Distinct()
                .Count();

            foreach (var r in vm.InLibraryDetails)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(r.VisitDate.ToString("yyyy-MM-dd"))}</td>");
                sb.Append($"<td>{HtmlEncode(r.StudentName)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Gender)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Purpose)}</td>");
                sb.Append($"<td>{HtmlEncode(r.BookTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.CatalogTitle)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Barcode)}</td>");
                sb.Append($"<td>{HtmlEncode(r.Status)}</td>");
                sb.Append($"<td>{HtmlEncode(r.ReturnedDate?.ToString("yyyy-MM-dd") ?? string.Empty)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("<tr>");
            sb.Append($"<td colspan='2'><b>{inLibStudents} unique student(s)</b></td>");
            sb.Append($"<td colspan='7'><b>{vm.InLibraryDetails.Count} item(s) read in library</b></td>");
            sb.Append("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            // ------------------------------------------------------------------
            // 3) Purchases
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>Purchases - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine(
                "<tr>" +
                "<th>Date</th>" +
                "<th>Supplier</th>" +
                "<th>Book Title</th>" +
                "<th>Quantity</th>" +
                "<th>Cost (៛)</th>" +
                "<th>Notes</th>" +
                "</tr>"
            );

            int totalQty = vm.PurchaseItems.Sum(x => x.Quantity);
            decimal totalCost = vm.PurchaseItems.Sum(x => x.Cost);

            foreach (var p in vm.PurchaseItems)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(p.PurchaseDate.ToString("yyyy-MM-dd"))}</td>");
                sb.Append($"<td>{HtmlEncode(p.Supplier)}</td>");
                sb.Append($"<td>{HtmlEncode(p.BookTitle)}</td>");
                sb.Append($"<td>{p.Quantity}</td>");
                sb.Append($"<td>{p.Cost:0}</td>");
                sb.Append($"<td>{HtmlEncode(p.Notes)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("<tr>");
            sb.Append($"<td colspan='3'><b>{vm.PurchaseItems.Count} row(s)</b></td>");
            sb.Append($"<td><b>{totalQty}</b></td>");
            sb.Append($"<td><b>{totalCost:0}</b></td>");
            sb.Append("<td></td>");
            sb.Append("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            // ------------------------------------------------------------------
            // 4) Adjustments (unchanged)
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>Adjustments - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");

            sb.AppendLine("<h4>Summary by Type</h4>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Type</th><th>Net Qty</th><th>Count</th></tr>");

            foreach (var s in vm.AdjustmentsByType)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(s.AdjustmentType)}</td>");
                sb.Append($"<td>{s.TotalQuantityChange}</td>");
                sb.Append($"<td>{s.Count}</td>");
                sb.Append("</tr>");
            }

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            sb.AppendLine("<h4>Details</h4>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Date</th><th>Type</th><th>Catalog Title</th><th>Qty Change</th><th>Reason</th></tr>");

            int totalAdjQty = vm.AdjustmentItems.Sum(x => x.QuantityChanged);

            foreach (var d in vm.AdjustmentItems)
            {
                sb.Append("<tr>");
                sb.Append($"<td>{HtmlEncode(d.AdjustmentDate.ToString("yyyy-MM-dd"))}</td>");
                sb.Append($"<td>{HtmlEncode(d.AdjustmentType)}</td>");
                sb.Append($"<td>{HtmlEncode(d.CatalogTitle)}</td>");
                sb.Append($"<td>{d.QuantityChanged}</td>");
                sb.Append($"<td>{HtmlEncode(d.Reason)}</td>");
                sb.Append("</tr>");
            }

            sb.Append("<tr>");
            sb.Append($"<td colspan='3'><b>{vm.AdjustmentItems.Count} row(s)</b></td>");
            sb.Append($"<td><b>{totalAdjQty}</b></td>");
            sb.Append("<td></td>");
            sb.Append("</tr>");

            sb.AppendLine("</table>");
            sb.AppendLine("<br />");

            // ------------------------------------------------------------------
            // 5) Financial Summary (use month-based sums already in VM)
            // ------------------------------------------------------------------
            sb.AppendLine($"<h3>Financial Summary - {HtmlEncode(vm.MonthName)} {vm.Year}</h3>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr><th>Category</th><th>Type</th><th>Amount (៛)</th></tr>");

            sb.Append("<tr><td>Borrowing fees</td><td>Income</td>");
            sb.Append($"<td>{vm.IncomeBorrowingFees:0}</td></tr>");

            sb.Append("<tr><td>Late fines</td><td>Income</td>");
            sb.Append($"<td>{vm.IncomeFines:0}</td></tr>");

            sb.Append("<tr><td>Extra charges (damage, etc.)</td><td>Income</td>");
            sb.Append($"<td>{vm.IncomeExtraCharges:0}</td></tr>");

            sb.Append("<tr><td>Book purchases</td><td>Expense</td>");
            sb.Append($"<td>{vm.ExpensePurchases:0}</td></tr>");

            sb.Append("<tr><td colspan='2'><b>Net Cash Flow (Income - Expense)</b></td>");
            sb.Append($"<td><b>{vm.NetCashFlow:0}</b></td></tr>");

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
