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
        //  GET: LoanReminders (5 per page)
        // ========================
        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 5;
            if (page < 1) page = 1;

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

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalReminders = totalReminders;

            return View(reminders);
        }

        // ========================
        //  GET: LoanReminders/Send
        // ========================
        public async Task<IActionResult> Send(int? loanId)
        {
            await BuildDropdownsAsync();

            var model = new LoanReminder();
            if (loanId.HasValue)
            {
                model.LoanId = loanId.Value;
            }

            return View(model);
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
                await BuildDropdownsAsync();
                return View(loanReminder);
            }

            // Load loan + member + books
            var loan = await _context.BookBorrows
                .Include(bl => bl.LibraryMember)
                .Include(bl => bl.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .FirstOrDefaultAsync(bl => bl.LoanId == loanReminder.LoanId);

            if (loan == null)
            {
                ModelState.AddModelError("LoanId", "Selected loan not found.");
                await BuildDropdownsAsync();
                return View(loanReminder);
            }

            var member = loan.LibraryMember;
            if (member == null)
            {
                ModelState.AddModelError("LoanId", "This loan has no member linked.");
                await BuildDropdownsAsync();
                return View(loanReminder);
            }

            if (string.IsNullOrWhiteSpace(member.TelegramChatId))
            {
                ModelState.AddModelError("LoanId", "This member has no Telegram Chat ID configured.");
                await BuildDropdownsAsync();
                return View(loanReminder);
            }

            // បង្កើតចំណងជើងសៀវភៅសង្ខេប (fallback Book.Title -> Catalog.Title)
            var titles = loan.LoanBookDetails
                .Where(d => d.Book != null)
                .Select(d =>
                    !string.IsNullOrWhiteSpace(d.Book!.Title)
                        ? d.Book.Title
                        : (d.Book.Catalog != null ? d.Book.Catalog.Title : null))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();

            var titleText = titles.Any()
                ? string.Join(", ", titles)
                : "(No book titles)";

            // គណនា ថ្ងៃហួស DueDate (overdue)
            var today = DateTime.Today;
            int overdueDays = (today - loan.DueDate.Date).Days;
            if (overdueDays < 0) overdueDays = 0;

            // បង្កើតសារ Telegram (Manual)
            string message =
                $"សួស្តី <b>{member.FullName}</b> 👋\n\n" +
                $"សៀវភៅដែលអ្នកបានខ្ចីពីបណ្ណាល័យ BBU មានកាលបរិច្ឆេទសង ចាប់តាំងពី <b>{loan.DueDate:yyyy-MM-dd}</b>" +
                $"{(overdueDays > 0 ? $" ហើយឥឡូវនេះហួសរយៈពេល <b>{overdueDays}</b> ថ្ងៃ។" : " និងឥឡូវនេះបានហួសកាលកំណត់។")}\n\n" +
                $"ចំណងជើងសៀវភៅ៖ {titleText}\n\n" +
                $"សូមយកមកប្រគល់វិញឲ្យបានឆាប់ តាមបណ្ណាល័យ BBU ដើម្បីជៀសវាងការបង់ពិន័យបន្ថែម។\n\n" +
                $"សូមអរគុណ🙏";

            // ផ្ញើ Telegram
            await _telegram.SendMessageAsync(member.TelegramChatId!, message);

            // កំណត់ SentDate
            loanReminder.SentDate = DateTime.Now;

            // ប្រសិនបើ ReminderType ទទេ => ដាក់ជា ManualTelegram
            if (string.IsNullOrWhiteSpace(loanReminder.ReminderType))
            {
                loanReminder.ReminderType = "ManualTelegram";
            }

            _context.LoanReminders.Add(loanReminder);
            await _context.SaveChangesAsync();

            TempData["ok"] = "Reminder has been sent via Telegram.";
            return RedirectToAction(nameof(Index));
        }

        // ========================
        //  Helper: Build dropdowns
        // ========================
        private async Task BuildDropdownsAsync()
        {
            var today = DateTime.Today;

            // 👉 Only require: not returned & overdue
            var overdueLoanEntities = await _context.BookBorrows
                .Where(bl =>
                    !bl.IsReturned &&
                    bl.DueDate < today &&
                    bl.LibraryMember != null)
                .Include(bl => bl.LibraryMember)
                .Include(bl => bl.LoanBookDetails)
                    .ThenInclude(d => d.Book)
                        .ThenInclude(b => b.Catalog)
                .AsNoTracking()
                .ToListAsync();

            var overdueLoans = overdueLoanEntities
                .Select(bl => new
                {
                    bl.LoanId,
                    bl.DueDate,
                    MemberName = bl.LibraryMember != null ? bl.LibraryMember.FullName : "(Unknown)",
                    Titles = bl.LoanBookDetails
                        .Where(d => d.Book != null)
                        .Select(d =>
                            !string.IsNullOrWhiteSpace(d.Book!.Title)
                                ? d.Book.Title
                                : (d.Book.Catalog != null ? d.Book.Catalog.Title : null))
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .Distinct()
                        .ToList()
                })
                .ToList();

            ViewBag.OverdueLoans = overdueLoans
                .Select(x => new SelectListItem
                {
                    Value = x.LoanId.ToString(),
                    Text = $"#{x.LoanId} – {string.Join(", ", x.Titles)} (Member: {x.MemberName}, Due: {x.DueDate:yyyy-MM-dd})"
                })
                .ToList();

            ViewBag.ReminderTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "ManualTelegram", Text = "Manual via Telegram" },
                new SelectListItem { Value = "ManualCall",     Text = "Manual Phone Call"  },
                new SelectListItem { Value = "ManualEmail",    Text = "Manual Email"      }
            };
        }
    }
}
