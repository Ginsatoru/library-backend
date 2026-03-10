using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using LibrarySystemBBU.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace LibrarySystemBBU.Controllers
{
    public class LoanRemindersController : Controller
    {
        private readonly DataContext _context;
        private readonly ITelegramService _telegram;

        public LoanRemindersController(DataContext context, ITelegramService telegram)
        {
            _context = context;
            _telegram = telegram;
        }

        // ========================
        //  GET: LoanReminders
        // ========================
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 10;
            if (page < 1) page = 1;

            var today = DateTime.Today;

            // Load overdue loans (not returned, past due date)
            var overdueLoans = await _context.BookBorrows
                .Where(bl => !bl.IsReturned && bl.DueDate < today)
                .Include(bl => bl.LibraryMember)
                .Include(bl => bl.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .AsNoTracking()
                .OrderBy(bl => bl.DueDate)
                .ToListAsync();

            // Load sent reminders (paginated)
            var baseQuery = _context.LoanReminders
                .Include(lr => lr.Loan)
                    .ThenInclude(bl => bl.LibraryMember)
                .Include(lr => lr.Loan)
                    .ThenInclude(bl => bl.LoanBookDetails)
                        .ThenInclude(d => d.Book)
                            .ThenInclude(b => b.Catalog)
                .OrderByDescending(lr => lr.SentDate)
                .AsNoTracking();

            var totalReminders = await baseQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalReminders / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var reminders = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.OverdueLoans = overdueLoans;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalReminders = totalReminders;


            return View(reminders);
        }

        // ========================
        //  POST: LoanReminders/Send
        // ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send([Bind("LoanId,ReminderType")] LoanReminder loanReminder)
        {
            if (!ModelState.IsValid)
            {
                TempData["error"] = "Invalid reminder data.";
                return RedirectToAction(nameof(Index));
            }

            var loan = await _context.BookBorrows
                .Include(bl => bl.LibraryMember)
                .Include(bl => bl.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(bl => bl.LoanId == loanReminder.LoanId);

            if (loan == null)
            {
                TempData["error"] = "Selected loan not found.";
                return RedirectToAction(nameof(Index));
            }

            var member = loan.LibraryMember;
            if (member == null)
            {
                TempData["error"] = "This loan has no member linked.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(member.TelegramChatId))
            {
                TempData["error"] = $"Member \"{member.FullName}\" has no Telegram Chat ID configured.";
                return RedirectToAction(nameof(Index));
            }

            var titles = loan.LoanBookDetails
                .Where(d => d.Book != null)
                .Select(d =>
                    !string.IsNullOrWhiteSpace(d.Book!.Title)
                        ? d.Book.Title
                        : (d.Book.Catalog != null ? d.Book.Catalog.Title : null))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            var titleText = titles.Any() ? string.Join(", ", titles) : "(No book titles)";

            var today = DateTime.Today;
            int overdueDays = Math.Max(0, (today - loan.DueDate.Date).Days);

            string message =
                $"សួស្តី <b>{member.FullName}</b> 👋\n\n" +
                $"សៀវភៅដែលអ្នកបានខ្ចីពីបណ្ណាល័យ BBU មានកាលបរិច្ឆេទសង ចាប់តាំងពី <b>{loan.DueDate:yyyy-MM-dd}</b>" +
                $"{(overdueDays > 0 ? $" ហើយឥឡូវនេះហួសរយៈពេល <b>{overdueDays}</b> ថ្ងៃ។" : " និងឥឡូវនេះបានហួសកាលកំណត់។")}\n\n" +
                $"ចំណងជើងសៀវភៅ៖ {titleText}\n\n" +
                $"សូមយកមកប្រគល់វិញឲ្យបានឆាប់ តាមបណ្ណាល័យ BBU ដើម្បីជៀសវាងការបង់ពិន័យបន្ថែម។\n\n" +
                $"សូមអរគុណ🙏";

            await _telegram.SendMessageAsync(member.TelegramChatId!, message);

            loanReminder.SentDate = DateTime.Now;
            if (string.IsNullOrWhiteSpace(loanReminder.ReminderType))
                loanReminder.ReminderType = "ManualTelegram";

            _context.LoanReminders.Add(loanReminder);
            await _context.SaveChangesAsync();

            TempData["ok"] = $"Reminder sent to {member.FullName} via Telegram.";
            return RedirectToAction(nameof(Index));
        }
    }
}