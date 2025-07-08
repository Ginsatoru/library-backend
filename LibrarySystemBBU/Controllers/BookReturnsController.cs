using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;

namespace LibrarySystemBBU.Controllers
{
    public class BookReturnsController : Controller
    {
        private readonly DataContext _context;

        public BookReturnsController(DataContext context)
        {
            _context = context;
        }

        // GET: BookReturns
        public async Task<IActionResult> Index()
        {
            var returns = await _context.BookReturns
                .Include(b => b.Loan)
                .ToListAsync();
            return View(returns);
        }

        // GET: BookReturns/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var bookReturn = await _context.BookReturns
                .Include(b => b.Loan)
                .FirstOrDefaultAsync(m => m.ReturnId == id);

            if (bookReturn == null)
                return NotFound();

            return View(bookReturn);
        }

        // GET: BookReturns/Create
        public IActionResult Create()
        {
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId");
            // Optionally set default ReturnDate to today
            var model = new BookReturn
            {
                ReturnDate = System.DateTime.Today
            };
            return View(model);
        }

        // POST: BookReturns/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReturnId,LoanId,ReturnDate,LateDays,FineAmount,AmountPaid,RefundAmount,ConditionOnReturn,Notes")] BookReturn bookReturn)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bookReturn);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Return record created successfully!";
                return RedirectToAction(nameof(Index));
            }
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", bookReturn.LoanId);
            return View(bookReturn);
        }

        // GET: BookReturns/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var bookReturn = await _context.BookReturns.FindAsync(id);
            if (bookReturn == null)
                return NotFound();

            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", bookReturn.LoanId);
            return View(bookReturn);
        }

        // POST: BookReturns/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReturnId,LoanId,ReturnDate,LateDays,FineAmount,AmountPaid,RefundAmount,ConditionOnReturn,Notes")] BookReturn bookReturn)
        {
            if (id != bookReturn.ReturnId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookReturn);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Return record updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookReturnExists(bookReturn.ReturnId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", bookReturn.LoanId);
            return View(bookReturn);
        }

        // GET: BookReturns/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var bookReturn = await _context.BookReturns
                .Include(b => b.Loan)
                .FirstOrDefaultAsync(m => m.ReturnId == id);

            if (bookReturn == null)
                return NotFound();

            return View(bookReturn);
        }

        // POST: BookReturns/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bookReturn = await _context.BookReturns.FindAsync(id);
            if (bookReturn != null)
            {
                _context.BookReturns.Remove(bookReturn);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Return record deleted successfully!";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool BookReturnExists(int id)
        {
            return _context.BookReturns.Any(e => e.ReturnId == id);
        }
    }
}
