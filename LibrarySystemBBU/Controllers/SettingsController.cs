using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace LibrarySystemBBU.Controllers
{
    [Authorize]
    [Route("Settings")]
    public class SettingsController : Controller
    {
        private readonly DataContext _context;
        private readonly IConfiguration _config;
        private const string BackupFolder = @"C:\LibraryBackups\";

        public SettingsController(DataContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var settings = await _context.LibrarySettings.FirstOrDefaultAsync()
                ?? new LibrarySettings();

            ViewBag.BackupFiles = GetBackupFiles();
            return View(settings);
        }

        [HttpPost("")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LibrarySettings model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.BackupFiles = GetBackupFiles();
                return View(model);
            }

            var existing = await _context.LibrarySettings.FirstOrDefaultAsync();
            if (existing == null)
            {
                _context.LibrarySettings.Add(model);
            }
            else
            {
                existing.Address = model.Address;
                existing.Email = model.Email;
                existing.Phone = model.Phone;
                existing.WeekdayHours = model.WeekdayHours;
                existing.WeekendHours = model.WeekendHours;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Settings saved.";
            return RedirectToAction(nameof(Index));
        }

        // ── Backup ────────────────────────────────────────────────
        [HttpPost("Backup")]
        [ValidateAntiForgeryToken]
        public IActionResult Backup()
        {
            try
            {
                if (!Directory.Exists(BackupFolder))
                    Directory.CreateDirectory(BackupFolder);

                var fileName = $"LibraryDB_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                var fullPath = Path.Combine(BackupFolder, fileName);
                var connStr = _config.GetConnectionString("DefaultConnection")!;

                using var conn = new SqlConnection(connStr);
                conn.Open();
                using var cmd = new SqlCommand(
                    $"BACKUP DATABASE [LibraryDB] TO DISK = N'{fullPath}' WITH FORMAT, INIT, NAME = 'LibraryDB Full Backup';",
                    conn);
                cmd.CommandTimeout = 120;
                cmd.ExecuteNonQuery();

                TempData["Success"] = $"Backup created: {fileName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Backup failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ── Restore ───────────────────────────────────────────────
        [HttpPost("Restore")]
        [ValidateAntiForgeryToken]
        public IActionResult Restore(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                TempData["Error"] = "No backup file selected.";
                return RedirectToAction(nameof(Index));
            }

            // Sanitize — only allow .bak files from the backup folder
            var safeName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(BackupFolder, safeName);

            if (!System.IO.File.Exists(fullPath) || !safeName.EndsWith(".bak"))
            {
                TempData["Error"] = "Invalid backup file.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var connStr = _config.GetConnectionString("DefaultConnection")!;

                // Connect to master to restore
                var masterConn = connStr.Replace("Database=LibraryDB", "Database=master",
                    StringComparison.OrdinalIgnoreCase);

                using var conn = new SqlConnection(masterConn);
                conn.Open();

                // Kill active connections first
                using (var killCmd = new SqlCommand(@"
                    ALTER DATABASE [LibraryDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;", conn))
                {
                    killCmd.CommandTimeout = 30;
                    killCmd.ExecuteNonQuery();
                }

                // Restore
                using (var restoreCmd = new SqlCommand(
                    $"RESTORE DATABASE [LibraryDB] FROM DISK = N'{fullPath}' WITH REPLACE;", conn))
                {
                    restoreCmd.CommandTimeout = 300;
                    restoreCmd.ExecuteNonQuery();
                }

                // Back to multi-user
                using (var multiCmd = new SqlCommand(
                    "ALTER DATABASE [LibraryDB] SET MULTI_USER;", conn))
                {
                    multiCmd.CommandTimeout = 30;
                    multiCmd.ExecuteNonQuery();
                }

                TempData["Success"] = $"Database restored from: {safeName}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Restore failed: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // ── Delete backup ─────────────────────────────────────────
        [HttpPost("DeleteBackup")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteBackup(string fileName)
        {
            var safeName = Path.GetFileName(fileName);
            var fullPath = Path.Combine(BackupFolder, safeName);

            if (System.IO.File.Exists(fullPath) && safeName.EndsWith(".bak"))
                System.IO.File.Delete(fullPath);

            TempData["Success"] = $"Deleted: {safeName}";
            return RedirectToAction(nameof(Index));
        }

        // ── Helper ────────────────────────────────────────────────
        private List<FileInfo> GetBackupFiles()
        {
            if (!Directory.Exists(BackupFolder)) return new();
            return new DirectoryInfo(BackupFolder)
                .GetFiles("*.bak")
                .OrderByDescending(f => f.CreationTime)
                .ToList();
        }
    }
}