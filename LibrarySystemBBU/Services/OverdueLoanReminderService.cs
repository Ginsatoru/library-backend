using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    // OverdueLoanReminderService: A background service that periodically checks for overdue books
    // and automatically creates LoanReminder records for them.
    public class OverdueLoanReminderService : IHostedService, IDisposable
    {
        private readonly ILogger<OverdueLoanReminderService> _logger;
        private readonly IServiceScopeFactory _scopeFactory; // Used to create scoped services (DataContext)
        private Timer? _timer = null; // Timer for periodic execution

        // Constructor: Injects logger and service scope factory.
        // IServiceScopeFactory is crucial because DataContext is typically scoped,
        // and background services are singletons. You need to create a new scope
        // for each operation to ensure correct DbContext lifecycle.
        public OverdueLoanReminderService(
            ILogger<OverdueLoanReminderService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        // StartAsync: Called when the application starts.
        // Initializes and starts the timer.
        public Task StartAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Overdue Loan Reminder Service is starting.");

            // Configure the timer to call DoWork every 1 minute (adjust as needed for production)
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1)); // Initial delay 0, then repeat every 1 minute

            return Task.CompletedTask;
        }

        // DoWork: The method executed by the timer.
        // It's responsible for finding overdue loans and creating reminders.
        private async void DoWork(object? state)
        {
            _logger.LogInformation("Overdue Loan Reminder Service is working. Checking for overdue loans...");

            // Create a new scope for the DataContext.
            // This ensures that DbContext is correctly disposed after each operation.
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();

                try
                {
                    // Get all loans that are not yet returned and are past their due date.
                    var overdueLoans = await context.BookLoans
                        .Where(bl => !bl.IsReturned && bl.DueDate < DateTime.Today)
                        .ToListAsync();

                    foreach (var loan in overdueLoans)
                    {
                        // Check if a "Overdue" reminder already exists for this loan.
                        // This prevents sending multiple identical reminders for the same loan.
                        bool reminderExists = await context.LoanReminders
                            .AnyAsync(lr => lr.LoanId == loan.LoanId && lr.ReminderType == "Overdue");

                        if (!reminderExists)
                        {
                            // Create a new LoanReminder record.
                            var reminder = new LoanReminder
                            {
                                LoanId = loan.LoanId,
                                SentDate = DateTime.Now, // Set the current date/time when reminder is generated
                                ReminderType = "Overdue" // Define a type for automated reminders
                            };

                            context.LoanReminders.Add(reminder);
                            _logger.LogInformation($"Created overdue reminder for LoanId: {loan.LoanId}");
                        }
                    }
                    await context.SaveChangesAsync(); // Save all new reminders
                    _logger.LogInformation("Overdue Loan Reminder Service finished checking.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while checking for overdue loans.");
                }
            }
        }

        // StopAsync: Called when the application is shutting down.
        // Stops the timer.
        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Overdue Loan Reminder Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0); // Stop the timer from firing

            return Task.CompletedTask;
        }

        // Dispose: Cleans up resources (the timer).
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
