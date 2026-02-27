using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibrarySystemBBU.Services
{
    /// <summary>
    /// Sends Telegram reminders:
    /// - 3 days before due date  (ReminderType = "DueIn3Days")
    /// - On the due date         (ReminderType = "DueToday")
    /// - 1 day after due date    (ReminderType = "Overdue1Day")
    /// </summary>
    public class OverdueLoanReminderService : IHostedService, IDisposable
    {
        private readonly ILogger<OverdueLoanReminderService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private Timer? _timer;

        public OverdueLoanReminderService(
            ILogger<OverdueLoanReminderService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📌 OverdueLoanReminderService is starting.");

            // Run immediately, then every 24 hours
            // (You can change TimeSpan.FromHours(24) to TimeSpan.FromMinutes(60) while testing)
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromHours(24));

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("🔎 OverdueLoanReminderService: Checking loans for reminders...");

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var telegram = scope.ServiceProvider.GetRequiredService<ITelegramService>();

            try
            {
                var today = DateTime.Today;

                // All active loans with Telegram chat
                var activeLoans = await context.BookBorrows
                    .Include(bl => bl.LibraryMember)
                    .Where(bl =>
                        !bl.IsReturned &&
                        bl.LibraryMember != null &&
                        !string.IsNullOrWhiteSpace(bl.LibraryMember.TelegramChatId))
                    .ToListAsync();

                _logger.LogInformation("Found {Count} active loans with Telegram chat.", activeLoans.Count);

                foreach (var loan in activeLoans)
                {
                    var member = loan.LibraryMember!;
                    var chatId = member.TelegramChatId!;

                    // daysDiff = positive if still before due date
                    // 0 = due today
                    // negative = already overdue
                    int daysDiff = (loan.DueDate.Date - today).Days;

                    string? reminderType = null;
                    string? msg = null;

                    if (daysDiff == 3)
                    {
                        // 3 days before due date
                        reminderType = "DueIn3Days";
                        msg =
                            $"សួស្តី <b>{member.FullName}</b> 👋\n\n" +
                            $"សៀវភៅដែលអ្នកបានខ្ចី នឹងដល់ថ្ងៃសងនៅក្នុង <b>3 ថ្ងៃទៀត</b> (កាលបរិច្ឆេទសង: {loan.DueDate:yyyy-MM-dd}).\n" +
                            $"សូមរៀបចំយកមកសងទាន់ពេល ដើម្បីជៀសវាងការបង់ពិន័យ 🙏";
                    }
                    else if (daysDiff == 0)
                    {
                        // Due date is TODAY
                        reminderType = "DueToday";
                        msg =
                            $"សួស្តី <b>{member.FullName}</b> 👋\n\n" +
                            $"សៀវភៅដែលអ្នកបានខ្ចី មានកាលបរិច្ឆេទសង <b>ថ្ងៃនេះ</b> ({loan.DueDate:yyyy-MM-dd}).\n" +
                            $"សូមយកមកសងនៅបណ្ណាល័យ BBU ក្នុងថ្ងៃនេះ ដើម្បីជៀសវាងការបង់ពិន័យ 🙏";
                    }
                    else if (daysDiff == -1)
                    {
                        // 1 day overdue
                        reminderType = "Overdue1Day";
                        msg =
                            $"សួស្តី <b>{member.FullName}</b> 👋\n\n" +
                            $"សៀវភៅដែលអ្នកបានខ្ចី បានហួសថ្ងៃសងចាប់តាំងពី <b>{loan.DueDate:yyyy-MM-dd}</b> (ឥឡូវនេះហួស <b>1 ថ្ងៃ</b>)។\n" +
                            $"សូមយកមកសងឲ្យបានឆាប់ តាមបណ្ណាល័យ BBU ដើម្បីកុំឲ្យមានពិន័យបន្ថែម 🙏";
                    }

                    // If this loan does not match any of these 3 cases, skip
                    if (reminderType == null || msg == null)
                        continue;

                    // Check if this reminder type already sent for this loan
                    bool alreadySent = await context.LoanReminders.AnyAsync(lr =>
                        lr.LoanId == loan.LoanId &&
                        lr.ReminderType == reminderType);

                    if (alreadySent)
                    {
                        _logger.LogInformation(
                            "Reminder '{ReminderType}' for LoanId {LoanId} already sent. Skipping.",
                            reminderType, loan.LoanId);
                        continue;
                    }

                    try
                    {
                        await telegram.SendMessageAsync(chatId, msg);
                    }
                    catch (Exception exTg)
                    {
                        _logger.LogError(exTg,
                            "Error sending Telegram message for LoanId {LoanId}.",
                            loan.LoanId);
                        // Continue with next loan
                        continue;
                    }

                    // Log to LoanReminders to avoid sending again
                    context.LoanReminders.Add(new LoanReminder
                    {
                        LoanId = loan.LoanId,
                        SentDate = DateTime.Now,
                        ReminderType = reminderType
                    });

                    await context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Reminder '{ReminderType}' sent for LoanId {LoanId} to Member {MemberName}.",
                        reminderType, loan.LoanId, member.FullName);
                }

                _logger.LogInformation("OverdueLoanReminderService: Reminder check complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OverdueLoanReminderService: Error occurred while checking loans.");
            }
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("📌 OverdueLoanReminderService is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}


