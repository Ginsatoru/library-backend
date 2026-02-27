using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LibrarySystemBBU.Controllers
{
    [Authorize(Roles = "Member")]
    public class MemberPortalController : Controller
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;
        private readonly IWebHostEnvironment _env;

        public MemberPortalController(DataContext context, IConfiguration config, IWebHostEnvironment env)
        {
            _context = context;
            _config = config;
            _env = env;
        }

        private async Task<Member?> GetCurrentMemberAsync()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(idStr)) return null;
            if (!Guid.TryParse(idStr, out var memberId)) return null;
            return await _context.Members.FirstOrDefaultAsync(m => m.MemberId == memberId);
        }

        // ----------------------------------------
        // GET /MemberPortal/ProfileJson
        // ----------------------------------------
        [HttpGet]
        public async Task<IActionResult> ProfileJson()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            return Ok(new
            {
                memberId = member.MemberId,
                fullName = member.FullName,
                email = member.Email,
                phone = member.Phone,
                address = member.Address,
                gender = member.Gender,
                memberType = member.MemberType,
                joinDate = member.JoinDate,
                isActive = member.IsActive,
                profilePicture = member.ProfilePicturePath,
                telegramChatId = member.TelegramChatId,
                telegramUsername = member.TelegramUsername,
                diCardNumber = member.DICardNumber,
                notes = member.Notes,
            });
        }

        // ----------------------------------------
        // POST /MemberPortal/UpdateProfileJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> UpdateProfileJson([FromBody] MemberUpdateRequest model)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            if (!string.IsNullOrWhiteSpace(model.FullName))
                member.FullName = model.FullName.Trim();

            member.Phone = model.Phone?.Trim();
            member.Address = model.Address?.Trim();
            member.Gender = model.Gender?.Trim();
            member.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ----------------------------------------
        // POST /MemberPortal/UpdateTelegramJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> UpdateTelegramJson([FromBody] TelegramUpdateRequest model)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            member.TelegramChatId = model.TelegramChatId?.Trim();
            member.TelegramUsername = model.TelegramUsername?.Trim().TrimStart('@');
            member.Modified = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // ----------------------------------------
        // POST /MemberPortal/UploadProfilePictureJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> UploadProfilePictureJson(IFormFile profilePicture)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            if (profilePicture == null || profilePicture.Length == 0)
                return Ok(new { success = false, message = "No file uploaded." });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return Ok(new { success = false, message = "Only image files are allowed." });

            if (profilePicture.Length > 10 * 1024 * 1024)
                return Ok(new { success = false, message = "File size must be less than 10MB." });

            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "members");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            if (!string.IsNullOrEmpty(member.ProfilePicturePath))
            {
                var oldPath = Path.Combine(_env.WebRootPath, member.ProfilePicturePath.TrimStart('/'));
                if (System.IO.File.Exists(oldPath)) System.IO.File.Delete(oldPath);
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await profilePicture.CopyToAsync(stream);

            member.ProfilePicturePath = $"/uploads/members/{fileName}";
            member.Modified = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, profilePicture = member.ProfilePicturePath });
        }

        // ----------------------------------------
        // POST /MemberPortal/ChangePasswordJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> ChangePasswordJson([FromBody] ChangePasswordRequest model)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(model.CurrentPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
                return Ok(new { success = false, message = "All fields are required." });

            if (!member.VerifyPassword(model.CurrentPassword))
                return Ok(new { success = false, message = "Current password is incorrect." });

            if (model.NewPassword != model.ConfirmNewPassword)
                return Ok(new { success = false, message = "Passwords do not match." });

            if (!member.TrySetPassword(model.NewPassword))
                return Ok(new { success = false, message = "Password must be 5–20 characters." });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Password changed successfully." });
        }

        // ----------------------------------------
        // GET /MemberPortal/HistoryJson
        // ----------------------------------------
        [HttpGet]
        public async Task<IActionResult> HistoryJson()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            var borrows = await _context.BookBorrows
                .Include(b => b.LoanBookDetails)
                    .ThenInclude(d => d.Catalog)
                .Include(b => b.BookReturns)
                .Where(b => b.MemberId == member.MemberId)
                .OrderByDescending(b => b.LoanDate)
                .Select(b => new {
                    loanId = b.LoanId,
                    loanDate = b.LoanDate,
                    dueDate = b.DueDate,
                    isReturned = b.IsReturned,
                    isPaid = b.IsPaid,
                    borrowingFee = b.BorrowingFee,
                    returnDate = b.BookReturns
                                    .OrderByDescending(r => r.ReturnDate)
                                    .Select(r => (DateTime?)r.ReturnDate)
                                    .FirstOrDefault(),
                    fineAmount = b.BookReturns
                                    .OrderByDescending(r => r.ReturnDate)
                                    .Select(r => (decimal?)r.FineAmount)
                                    .FirstOrDefault(),
                    books = b.LoanBookDetails.Select(d => new {
                        catalogTitle = d.Catalog != null ? d.Catalog.Title : "Unknown",
                        conditionOut = d.ConditionOut,
                        conditionIn = d.ConditionIn,
                    }).ToList()
                })
                .ToListAsync();

            var histories = await _context.Histories
                .Where(h => h.MemberName == member.FullName)
                .OrderByDescending(h => h.OccurredUtc)
                .Select(h => new {
                    entityType = h.EntityType,
                    actionType = h.ActionType,
                    bookTitle = h.BookTitle ?? h.CatalogTitle,
                    occurredUtc = h.OccurredUtc,
                    loanDate = h.LoanDate,
                    dueDate = h.DueDate,
                    returnDate = h.ReturnDate,
                    fineAmount = h.FineAmount,
                    amountPaid = h.AmountPaid,
                    notes = h.Notes,
                })
                .ToListAsync();

            return Ok(new { borrows, histories });
        }

        // ----------------------------------------
        // GET /MemberPortal/ConnectTelegram
        // ----------------------------------------
        [HttpGet]
        public async Task<IActionResult> ConnectTelegram()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null)
                return RedirectToAction("Login", "MemberAuth");

            if (string.IsNullOrWhiteSpace(member.TelegramPairToken))
            {
                member.TelegramPairToken = Guid.NewGuid().ToString("N").Substring(0, 10);
                await _context.SaveChangesAsync();
            }

            var botUsername = _config["Telegram:BotUsername"] ?? "YourLibraryBot";
            var deepLink = $"https://t.me/{botUsername}?start={member.TelegramPairToken}";
            ViewBag.TelegramDeepLink = deepLink;

            return View(member);
        }

        // ----------------------------------------
        // GET /MemberPortal/WishlistJson
        // ----------------------------------------
        [HttpGet]
        public async Task<IActionResult> WishlistJson()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            var items = await _context.MemberWishlists
                .Where(w => w.MemberId == member.MemberId)
                .Include(w => w.Catalog)
                .OrderByDescending(w => w.AddedAt)
                .Select(w => new
                {
                    wishlistId = w.WishlistId,
                    addedAt = w.AddedAt,
                    catalogId = w.Catalog.CatalogId,
                    title = w.Catalog.Title,
                    author = w.Catalog.Author,
                    isbn = w.Catalog.ISBN,
                    category = w.Catalog.Category,
                    totalCopies = w.Catalog.TotalCopies,
                    availableCopies = w.Catalog.AvailableCopies,
                    borrowCount = w.Catalog.BorrowCount,
                    inLibraryCount = w.Catalog.InLibraryCount,
                    imagePath = w.Catalog.ImagePath,
                    hasPdf = !string.IsNullOrEmpty(w.Catalog.PdfFilePath)
                })
                .ToListAsync();

            return Ok(new { success = true, data = items });
        }

        // ----------------------------------------
        // GET /MemberPortal/WishlistIdsJson
        // ----------------------------------------
        [HttpGet]
        public async Task<IActionResult> WishlistIdsJson()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Ok(new { success = true, data = Array.Empty<Guid>() });

            var ids = await _context.MemberWishlists
                .Where(w => w.MemberId == member.MemberId)
                .Select(w => w.CatalogId)
                .ToListAsync();

            return Ok(new { success = true, data = ids });
        }

        // ----------------------------------------
        // POST /MemberPortal/AddToWishlistJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> AddToWishlistJson([FromBody] WishlistRequest model)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            if (model == null || model.CatalogId == Guid.Empty)
                return BadRequest(new { success = false, message = "Invalid catalog." });

            var alreadyExists = await _context.MemberWishlists
                .AnyAsync(w => w.MemberId == member.MemberId && w.CatalogId == model.CatalogId);

            if (alreadyExists)
                return Ok(new { success = true, message = "Already in wishlist." });

            var catalogExists = await _context.Catalogs.AnyAsync(c => c.CatalogId == model.CatalogId);
            if (!catalogExists)
                return NotFound(new { success = false, message = "Book not found." });

            _context.MemberWishlists.Add(new MemberWishlist
            {
                MemberId = member.MemberId,
                CatalogId = model.CatalogId,
                AddedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Added to wishlist." });
        }

        // ----------------------------------------
        // POST /MemberPortal/RemoveFromWishlistJson
        // ----------------------------------------
        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlistJson([FromBody] WishlistRequest model)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return Unauthorized();

            if (model == null || model.CatalogId == Guid.Empty)
                return BadRequest(new { success = false, message = "Invalid catalog." });

            var item = await _context.MemberWishlists
                .FirstOrDefaultAsync(w => w.MemberId == member.MemberId && w.CatalogId == model.CatalogId);

            if (item == null)
                return Ok(new { success = true, message = "Not in wishlist." });

            _context.MemberWishlists.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Removed from wishlist." });
        }

        // ---- Request Models ----
        public class MemberUpdateRequest
        {
            public string? FullName { get; set; }
            public string? Phone { get; set; }
            public string? Address { get; set; }
            public string? Gender { get; set; }
        }

        public class TelegramUpdateRequest
        {
            public string? TelegramChatId { get; set; }
            public string? TelegramUsername { get; set; }
        }

        public class ChangePasswordRequest
        {
            public string? CurrentPassword { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmNewPassword { get; set; }
        }

        public class WishlistRequest
        {
            public Guid CatalogId { get; set; }
        }
    }
}