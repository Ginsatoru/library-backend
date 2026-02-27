using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;

namespace LibrarySystemBBU.Controllers
{
    public class MembersController : Controller
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _env;

        public MembersController(DataContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ----------------- CURRENT USER (as Users entity) -----------------
        private async Task<Users?> GetCurrentUserAsync()
        {
            var uname = User?.Identity?.Name;
            if (string.IsNullOrWhiteSpace(uname)) return null;
            return await _context.Users.FirstOrDefaultAsync(u => u.UserName == uname);
        }

        // GET: Members
        public async Task<IActionResult> Index()
        {
            var dataContext = _context.Members
                .Include(m => m.Users)
                .Include(m => m.BookLoans);

            return View(await dataContext.ToListAsync());
        }

        // GET: Members/Details/5
        public async Task<IActionResult> Details(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members
                .Include(m => m.Users)
                .FirstOrDefaultAsync(m => m.MemberId == id);

            if (member == null) return NotFound();

            return View(member);
        }

        // GET: Members/Create
        public IActionResult Create()
        {
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName");
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName");
            return View();
        }

        // POST: Members/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("FullName,Gender,Email,Phone,Address,MemberType,IsActive,Notes,UserId,CreatedBy,DICardNumber,TelegramChatId,TelegramUsername")]
            Member member,
            IFormFile? ProfilePicture,
            string? NewPassword,
            string? ConfirmPassword)
        {
            // Validate password pair if provided
            if (!string.IsNullOrWhiteSpace(NewPassword) || !string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword) || NewPassword != ConfirmPassword)
                    ModelState.AddModelError("NewPassword", "Password and Confirm Password must match.");
                else if (NewPassword.Length < 5 || NewPassword.Length > 20)
                    ModelState.AddModelError("NewPassword", "Password length must be 5 - 20 characters.");
            }

            // ✅ Make photo optional
            ModelState.Remove(nameof(Member.ProfilePicturePath));
            ModelState.Remove("ProfilePicturePath");

            if (!ModelState.IsValid)
            {
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                return View(member);
            }

            member.MemberId = Guid.NewGuid();
            member.JoinDate = DateTime.UtcNow;
            member.Modified = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(NewPassword))
                member.TrySetPassword(NewPassword);

            try
            {
                if (ProfilePicture != null && ProfilePicture.Length > 0)
                    member.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("ProfilePicturePath", "Image upload failed: " + ex.Message);
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                return View(member);
            }

            _context.Add(member);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Members/Edit/5
        public async Task<IActionResult> Edit(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
            ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
            return View(member);
        }

        // ✅ POST: Members/Edit/5 (NO current password required)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            Guid id,
            IFormFile? ProfilePicture,
            string? NewPassword,
            string? ConfirmPassword)
        {
            var member = await _context.Members.FirstOrDefaultAsync(m => m.MemberId == id);
            if (member == null) return NotFound();

            // Apply posted values onto existing member (only allowed fields)
            var updateOk = await TryUpdateModelAsync(
                member,
                prefix: "",
                m => m.FullName,
                m => m.Gender,
                m => m.Email,
                m => m.Phone,
                m => m.Address,
                m => m.MemberType,
                m => m.IsActive,
                m => m.Notes,
                m => m.UserId,
                m => m.CreatedBy,
                m => m.DICardNumber,
                m => m.TelegramChatId,
                m => m.TelegramUsername
            );

            if (!updateOk)
            {
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                return View(member);
            }

            // password change requested?
            bool isChangingPassword = !string.IsNullOrWhiteSpace(NewPassword) || !string.IsNullOrWhiteSpace(ConfirmPassword);

            if (isChangingPassword)
            {
                // validate new + confirm
                if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword) || NewPassword != ConfirmPassword)
                {
                    ModelState.AddModelError("NewPassword", "Password and Confirm Password must match.");
                    TempData["PwdError"] = "Password and Confirm Password must match.";
                }
                else if (NewPassword.Length < 5 || NewPassword.Length > 20)
                {
                    ModelState.AddModelError("NewPassword", "Password length must be 5 - 20 characters.");
                    TempData["PwdError"] = "Password length must be 5 - 20 characters.";
                }
            }

            // ✅ Make photo optional on edit as well
            ModelState.Remove(nameof(Member.ProfilePicturePath));
            ModelState.Remove("ProfilePicturePath");

            if (!ModelState.IsValid)
            {
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                return View(member);
            }

            member.Modified = DateTime.UtcNow;

            // ✅ Apply new password WITHOUT current password
            if (isChangingPassword && !string.IsNullOrWhiteSpace(NewPassword))
            {
                if (!member.TrySetPassword(NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "Invalid password.");
                    TempData["PwdError"] = "Invalid new password. Your password was not updated.";
                    ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                    ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                    return View(member);
                }

                var actor = await GetCurrentUserAsync();
                member.LastPasswordResetByUserId = actor?.Id;
                member.LastPasswordResetAt = DateTime.UtcNow;
            }

            // Handle profile picture
            try
            {
                if (ProfilePicture != null && ProfilePicture.Length > 0)
                {
                    if (!string.IsNullOrEmpty(member.ProfilePicturePath))
                        DeleteProfilePicture(member.ProfilePicturePath);

                    member.ProfilePicturePath = await SaveProfilePicture(ProfilePicture);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("ProfilePicturePath", "Image upload failed: " + ex.Message);
                ViewData["UserId"] = new SelectList(_context.Users, "Id", "UserName", member.UserId);
                ViewData["CreatedByList"] = new SelectList(_context.Users, "UserName", "UserName", member.CreatedBy);
                return View(member);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MemberExists(member.MemberId))
                    return NotFound();
                else
                    throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // =============== RESET PASSWORD (permission-aware) ===============
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ResetPassword(Guid id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            var actor = await GetCurrentUserAsync();
            if (!Member.CanResetPassword(actor, member)) return Forbid();

            ViewBag.MemberName = member.FullName;
            return View(model: id);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(Guid id, string NewPassword, string ConfirmPassword)
        {
            var member = await _context.Members.FindAsync(id);
            if (member == null) return NotFound();

            var actor = await GetCurrentUserAsync();
            if (!Member.CanResetPassword(actor, member)) return Forbid();

            if (string.IsNullOrWhiteSpace(NewPassword) || string.IsNullOrWhiteSpace(ConfirmPassword) || NewPassword != ConfirmPassword)
            {
                ViewBag.MemberName = member.FullName;
                ModelState.AddModelError("NewPassword", "Password and Confirm Password must match.");
                return View(model: id);
            }
            if (NewPassword.Length < 5 || NewPassword.Length > 20)
            {
                ViewBag.MemberName = member.FullName;
                ModelState.AddModelError("NewPassword", "Password length must be 5 - 20 characters.");
                return View(model: id);
            }

            if (!member.TrySetPassword(NewPassword))
            {
                ViewBag.MemberName = member.FullName;
                ModelState.AddModelError("NewPassword", "Invalid password.");
                return View(model: id);
            }

            member.ClearPasswordResetToken();
            member.MarkPasswordResetBy(actor);

            await _context.SaveChangesAsync();

            TempData["ok"] = "Member password has been reset.";
            return RedirectToAction("Details", new { id = member.MemberId });
        }

        // GET: Members/Delete/5
        public async Task<IActionResult> Delete(Guid? id)
        {
            if (id == null) return NotFound();

            var member = await _context.Members
                .Include(m => m.Users)
                .Include(m => m.BookLoans)
                .FirstOrDefaultAsync(m => m.MemberId == id);

            if (member == null) return NotFound();

            return View(member);
        }

        // POST: Members/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var member = await _context.Members.FindAsync(id);
            if (member != null)
            {
                if (!string.IsNullOrEmpty(member.ProfilePicturePath))
                    DeleteProfilePicture(member.ProfilePicturePath);

                _context.Members.Remove(member);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool MemberExists(Guid id)
        {
            return _context.Members.Any(e => e.MemberId == id);
        }

        // ----------- IMAGE UPLOAD HELPERS --------------
        private async Task<string> SaveProfilePicture(IFormFile profilePicture)
        {
            var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "members");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(profilePicture.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new InvalidOperationException("Only image files (.jpg, .jpeg, .png, .gif, .webp) are allowed.");

            if (profilePicture.Length > 10 * 1024 * 1024)
                throw new InvalidOperationException("File size must be less than 10MB.");

            var uniqueFileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            return $"/uploads/members/{uniqueFileName}";
        }

        private void DeleteProfilePicture(string profilePicturePath)
        {
            if (string.IsNullOrEmpty(profilePicturePath)) return;
            var filePath = Path.Combine(_env.WebRootPath, profilePicturePath.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
        }

        // ================== EXPORT (CSV / XLS) ==================
        [HttpGet]
        public async Task<IActionResult> Export(string format = "csv")
        {
            var data = await _context.Members
                .AsNoTracking()
                .Include(m => m.Users)
                .Include(m => m.BookLoans)
                .OrderBy(m => m.FullName)
                .ToListAsync();

            if (string.Equals(format, "xls", StringComparison.OrdinalIgnoreCase))
            {
                var html = BuildExcelHtml(data);
                var bytes = AddUtf8Bom(html);
                return File(bytes, "application/vnd.ms-excel; charset=utf-8", "Members.xls");
            }

            var csv = BuildCsv(data);
            var csvBytes = AddUtf8Bom(csv);
            return File(csvBytes, "text/csv; charset=utf-8", "Members.csv");
        }

        // ----------------- CSV / HTML helpers -----------------
        private static string CsvEscape(string? s) =>
            "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";

        private static string CsvText(string? s) =>
            $"=\"{(s ?? string.Empty).Replace("\"", "\"\"")}\"";

        private static string CsvDateText(DateTime? dt, string fmt) =>
            dt.HasValue ? CsvText(dt.Value.ToString(fmt)) : "\"\"";

        private static string HtmlEncode(string? s) =>
            System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

        private string BuildCsv(System.Collections.Generic.IEnumerable<Member> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("MemberId,FullName,UserName,Gender,Email,Phone,MemberType,IsActive,JoinDate,Modified,DICardNumber,TelegramChatId,TelegramUsername,TotalLoans,ActiveLoans");

            foreach (var m in rows)
            {
                var totalLoans = m.BookLoans?.Count ?? 0;
                var activeLoans = m.BookLoans?.Count(b => b != null && !b.IsReturned) ?? 0;

                sb.Append(string.Join(",", new[]
                {
                    CsvEscape(m.MemberId.ToString()),
                    CsvEscape(m.FullName),
                    CsvEscape(m.Users?.UserName),
                    CsvEscape(m.Gender),
                    CsvEscape(m.Email),
                    CsvText(m.Phone),
                    CsvEscape(m.MemberType),
                    CsvEscape(m.IsActive ? "Active" : "Inactive"),
                    CsvDateText(m.JoinDate, "yyyy-MM-dd"),
                    CsvDateText(m.Modified, "yyyy-MM-dd HH:mm"),
                    CsvEscape(m.DICardNumber),
                    CsvEscape(m.TelegramChatId),
                    CsvEscape(m.TelegramUsername),
                    CsvEscape(totalLoans.ToString()),
                    CsvEscape(activeLoans.ToString())
                }));
                sb.Append("\r\n");
            }

            return sb.ToString();
        }

        private string BuildExcelHtml(System.Collections.Generic.IEnumerable<Member> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset=\"UTF-8\"></head><body>");
            sb.AppendLine("<table border='1' cellspacing='0' cellpadding='4'>");
            sb.AppendLine("<tr>" +
                          "<th>MemberId</th>" +
                          "<th>FullName</th>" +
                          "<th>UserName</th>" +
                          "<th>Gender</th>" +
                          "<th>Email</th>" +
                          "<th>Phone</th>" +
                          "<th>MemberType</th>" +
                          "<th>Status</th>" +
                          "<th>JoinDate</th>" +
                          "<th>Modified</th>" +
                          "<th>DICardNumber</th>" +
                          "<th>TelegramChatId</th>" +
                          "<th>TelegramUsername</th>" +
                          "<th>TotalLoans</th>" +
                          "<th>ActiveLoans</th>" +
                          "</tr>");

            foreach (var m in rows)
            {
                var totalLoans = m.BookLoans?.Count ?? 0;
                var activeLoans = m.BookLoans?.Count(b => b != null && !b.IsReturned) ?? 0;

                string tdText(string? v) =>
                    $"<td style='mso-number-format:\\@'>{HtmlEncode(v)}</td>";

                string tdDate(DateTime? d, string f) =>
                    d.HasValue
                        ? $"<td style='mso-number-format:\\@'>{HtmlEncode(d.Value.ToString(f))}</td>"
                        : "<td></td>";

                sb.Append("<tr>");
                sb.Append(tdText(m.MemberId.ToString()));
                sb.Append(tdText(m.FullName));
                sb.Append(tdText(m.Users?.UserName));
                sb.Append(tdText(m.Gender));
                sb.Append(tdText(m.Email));
                sb.Append(tdText(m.Phone));
                sb.Append(tdText(m.MemberType));
                sb.Append(tdText(m.IsActive ? "Active" : "Inactive"));
                sb.Append(tdDate(m.JoinDate, "yyyy-MM-dd"));
                sb.Append(tdDate(m.Modified, "yyyy-MM-dd HH:mm"));
                sb.Append(tdText(m.DICardNumber));
                sb.Append(tdText(m.TelegramChatId));
                sb.Append(tdText(m.TelegramUsername));
                sb.Append(tdText(totalLoans.ToString()));
                sb.Append(tdText(activeLoans.ToString()));
                sb.Append("</tr>");
            }

            sb.AppendLine("</table></body></html>");
            return sb.ToString();
        }

        private static byte[] AddUtf8Bom(string s)
        {
            var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            return utf8Bom.GetBytes(s);
        }
    }
}
