using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Newtonsoft.Json;

namespace LibrarySystemBBU.Controllers
{
    public class BookLoansController : Controller
    {
        private readonly DataContext _context;

        public BookLoansController(DataContext context)
        {
            _context = context;
        }

        // GET: BookLoans
        public async Task<IActionResult> Index()
        {
            var bookLoans = _context.BookLoans.Include(b => b.LibraryMember);
            return View(await bookLoans.ToListAsync());
        }

        // GET: BookLoans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bookLoan = await _context.BookLoans
                .Include(b => b.LibraryMember)
                .FirstOrDefaultAsync(m => m.LoanId == id);

            if (bookLoan == null) return NotFound();

            return View(bookLoan);
        }

        // GET: BookLoans/Create
        public IActionResult Create()
        {
            // For autocomplete inline input
            var memberList = _context.Members
                .Select(m => new
                {
                    label = m.FullName, // Autocomplete label
                    value = m.MemberId  // Autocomplete value
                }).ToList();

            ViewBag.Members = memberList;

            // If you still want classic dropdown for other cases:
            ViewData["MemberId"] = new SelectList(_context.Members, "MemberId", "FullName");
            return View();
        }

        // POST: BookLoans/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LoanId,MemberId,LoanDate,DueDate,IsReturned,BorrowingFee,IsPaid,DepositAmount")] BookLoan bookLoan)
        {
            // You can also retrieve MemberId from Request.Form if using hidden input:
            // var memberIdFromAutocomplete = Request.Form["MemberId"];
            // if (!string.IsNullOrEmpty(memberIdFromAutocomplete)) bookLoan.MemberId = int.Parse(memberIdFromAutocomplete);

            if (ModelState.IsValid)
            {
                _context.Add(bookLoan);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            // Same as GET:
            var memberList = _context.Members
                .Select(m => new
                {
                    label = m.FullName,
                    value = m.MemberId
                }).ToList();
            ViewBag.Members = memberList;
            ViewData["MemberId"] = new SelectList(_context.Members, "MemberId", "FullName", bookLoan.MemberId);

            return View(bookLoan);
        }

        // GET: BookLoans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bookLoan = await _context.BookLoans.FindAsync(id);
            if (bookLoan == null) return NotFound();

            // For edit dropdown
            ViewData["MemberId"] = new SelectList(_context.Members, "MemberId", "FullName", bookLoan.MemberId);

            // For edit inline autocomplete (if you want)
            var memberList = _context.Members
                .Select(m => new
                {
                    label = m.FullName,
                    value = m.MemberId
                }).ToList();
            ViewBag.Members = memberList;

            return View(bookLoan);
        }

        // POST: BookLoans/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LoanId,MemberId,LoanDate,DueDate,IsReturned,BorrowingFee,IsPaid,DepositAmount")] BookLoan bookLoan)
        {
            if (id != bookLoan.LoanId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookLoan);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookLoanExists(bookLoan.LoanId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // For edit dropdown
            ViewData["MemberId"] = new SelectList(_context.Members, "MemberId", "FullName", bookLoan.MemberId);

            // For edit inline autocomplete (if you want)
            var memberList = _context.Members
                .Select(m => new
                {
                    label = m.FullName,
                    value = m.MemberId
                }).ToList();
            ViewBag.Members = memberList;

            return View(bookLoan);
        }

        // GET: BookLoans/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bookLoan = await _context.BookLoans
                .Include(b => b.LibraryMember)
                .FirstOrDefaultAsync(m => m.LoanId == id);

            if (bookLoan == null) return NotFound();

            return View(bookLoan);
        }

        // POST: BookLoans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookLoan = await _context.BookLoans.FindAsync(id);
            if (bookLoan != null)
            {
                _context.BookLoans.Remove(bookLoan);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: BookLoans/MarkReturned/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkReturned(int id)
        {
            var loan = await _context.BookLoans.FindAsync(id);
            if (loan != null && !loan.IsReturned)
            {
                loan.IsReturned = true;
                _context.Update(loan);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BookLoanExists(int id)
        {
            return _context.BookLoans.Any(e => e.LoanId == id);
        }
    }
}
