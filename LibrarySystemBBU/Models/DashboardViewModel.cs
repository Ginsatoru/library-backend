using System.Collections.Generic;

namespace LibrarySystemBBU.Models
{
    public class DashboardViewModel
    {
        public int TotalBooks { get; set; }
        public int TotalLoans { get; set; }
        public int PendingReturns { get; set; }
        public int OverdueLoans { get; set; }
        public int ReturnedBooks { get; set; }
        public int TotalMembers { get; set; }
        public int TotalBookTypes { get; set; }

        // Added for dashboard summary
        public int Purchases { get; set; }       // Total Purchases
        public int NotReturn { get; set; }       // Total Not Return
        // ReturnedBooks is already here; it means Total Returned

        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlyLoanStats { get; set; } = new();
        public List<int> MonthlyMemberStats { get; set; } = new();

        public List<string> DailyLabels { get; set; } = new();
        public List<int> DailyLoanStats { get; set; } = new();
    }
}
