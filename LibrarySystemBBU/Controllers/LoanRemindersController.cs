// In your Controllers folder: LoanRemindersController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Controllers
{
    public class LoanRemindersController : Controller
    {
        private readonly DataContext _context;

        public LoanRemindersController(DataContext context)
        {
            _context = context;
        }

        // GET: LoanReminders
        public async Task<IActionResult> Index()
        {
            var reminders = await _context.LoanReminders
                                          .Include(lr => lr.Loan)
                                              .ThenInclude(bl => bl.LibraryMember)
                                          .Include(lr => lr.Loan)
                                              .ThenInclude(bl => bl.Book)
                                          .OrderByDescending(lr => lr.SentDate)
                                          .ToListAsync();
            return View(reminders);
        }

        // GET: LoanReminders/Send (Form to send a new reminder)
        public async Task<IActionResult> Send()
        {
            // Get all overdue and unreturned loans
            var overdueLoans = await _context.BookLoans
                                             .Where(bl => !bl.IsReturned && bl.DueDate < DateTime.Today)
                                             .Include(bl => bl.LibraryMember)
                                             .Include(bl => bl.Book)
                                             .Select(bl => new SelectListItem
                                             {
                                                 Value = bl.LoanId.ToString(),
                                                 Text = $"{bl.LoanId} - {bl.Book!.Title} (Member: {bl.LibraryMember!.FullName}, Due: {bl.DueDate.ToShortDateString()})"
                                             })
                                             .ToListAsync();
            ViewBag.OverdueLoans = overdueLoans;

            // Example reminder types
            ViewBag.ReminderTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Email", Text = "Email" },
                new SelectListItem { Value = "SMS", Text = "SMS" },
                new SelectListItem { Value = "Call", Text = "Phone Call" }
            };

            return View();
        }

        // POST: LoanReminders/Send
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send([Bind("LoanId,ReminderType,SentDate")] LoanReminder loanReminder)
        {
            if (ModelState.IsValid)
            {
                // Set SentDate to now if not provided by form (though typically handled by HasDefaultValueSql)
                loanReminder.SentDate = DateTime.Now;
                _context.Add(loanReminder);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // If model state is not valid, re-populate dropdowns and return view
            ViewBag.OverdueLoans = await _context.BookLoans
                                             .Where(bl => !bl.IsReturned && bl.DueDate < DateTime.Today)
                                             .Include(bl => bl.LibraryMember)
                                             .Include(bl => bl.Book)
                                             .Select(bl => new SelectListItem
                                             {
                                                 Value = bl.LoanId.ToString(),
                                                 Text = $"{bl.LoanId} - {bl.Book!.Title} (Member: {bl.LibraryMember!.FullName}, Due: {bl.DueDate.ToShortDateString()})"
                                             })
                                             .ToListAsync();

            ViewBag.ReminderTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = "Email", Text = "Email" },
                new SelectListItem { Value = "SMS", Text = "SMS" },
                new SelectListItem { Value = "Call", Text = "Phone Call" }
            };

            return View(loanReminder);
        }
    }
}