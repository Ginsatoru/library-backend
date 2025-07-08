using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;

namespace LibrarySystemBBU.Controllers
{
    public class LoanBookDetailsController : Controller
    {
        private readonly DataContext _context;

        public LoanBookDetailsController(DataContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Display all loan book details.
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var loanBookDetailsQuery = _context.LoanBookDetails
                .Include(l => l.Book)
                .Include(l => l.Catalog)
                .Include(l => l.Loan);
            return View(await loanBookDetailsQuery.ToListAsync());
        }

        /// <summary>
        /// Details page for one record.
        /// </summary>
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var loanBookDetail = await _context.LoanBookDetails
                .Include(l => l.Book)
                .Include(l => l.Catalog)
                .Include(l => l.Loan)
                .FirstOrDefaultAsync(m => m.LoanBookDetailId == id);

            if (loanBookDetail == null)
                return NotFound();

            return View(loanBookDetail);
        }

        /// <summary>
        /// Show Create form.
        /// </summary>
        public IActionResult Create()
        {
            // Choose best display field for dropdowns:
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title"); // Or "Author" if Title not present
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode");
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId");
            return View();
        }

        /// <summary>
        /// Create POST.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LoanBookDetailId,LoanId,CatalogId,BookId,ConditionOut,ConditionIn,FineDetailAmount,FineDetailReason,Created,Modified")] LoanBookDetail loanBookDetail)
        {
            if (ModelState.IsValid)
            {
                _context.Add(loanBookDetail);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", loanBookDetail.BookId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", loanBookDetail.CatalogId);
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", loanBookDetail.LoanId);
            return View(loanBookDetail);
        }

        /// <summary>
        /// Edit GET
        /// </summary>
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var loanBookDetail = await _context.LoanBookDetails.FindAsync(id);
            if (loanBookDetail == null)
                return NotFound();

            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", loanBookDetail.BookId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", loanBookDetail.CatalogId);
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", loanBookDetail.LoanId);
            return View(loanBookDetail);
        }

        /// <summary>
        /// Edit POST
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("LoanBookDetailId,LoanId,CatalogId,BookId,ConditionOut,ConditionIn,FineDetailAmount,FineDetailReason,Created,Modified")] LoanBookDetail loanBookDetail)
        {
            if (id != loanBookDetail.LoanBookDetailId)
                return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(loanBookDetail);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LoanBookDetailExists(loanBookDetail.LoanBookDetailId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookId"] = new SelectList(_context.Books, "BookId", "Title", loanBookDetail.BookId);
            ViewData["CatalogId"] = new SelectList(_context.Catalogs, "CatalogId", "Barcode", loanBookDetail.CatalogId);
            ViewData["LoanId"] = new SelectList(_context.BookLoans, "LoanId", "LoanId", loanBookDetail.LoanId);
            return View(loanBookDetail);
        }

        /// <summary>
        /// Delete GET
        /// </summary>
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var loanBookDetail = await _context.LoanBookDetails
                .Include(l => l.Book)
                .Include(l => l.Catalog)
                .Include(l => l.Loan)
                .FirstOrDefaultAsync(m => m.LoanBookDetailId == id);
            if (loanBookDetail == null)
                return NotFound();

            return View(loanBookDetail);
        }

        /// <summary>
        /// Delete POST
        /// </summary>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var loanBookDetail = await _context.LoanBookDetails.FindAsync(id);
            if (loanBookDetail != null)
            {
                _context.LoanBookDetails.Remove(loanBookDetail);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LoanBookDetailExists(int id)
        {
            return _context.LoanBookDetails.Any(e => e.LoanBookDetailId == id);
        }
    }
}
