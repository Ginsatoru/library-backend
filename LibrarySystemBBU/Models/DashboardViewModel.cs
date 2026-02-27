using System.Collections.Generic;

namespace LibrarySystemBBU.Models
{
    public class DashboardViewModel
    {
        // ===== Canonical (ថ្មី) =====
        public int TotalCatalog { get; set; }     
        public int TotalMembers { get; set; }
        public int TotalBooks { get; set; }         
        public int TotalBorrows { get; set; }       
        public int OverdueBorrows { get; set; }    
        public int ReturnedBorrows { get; set; }    
        public int NotReturn { get; set; }        
        public int PendingReturns { get; set; }
        public int Purchases { get; set; }

        // NEW: total adjustments (for KPI + Excel)
        public int Adjustments { get; set; }

        public List<string> MonthlyLabels { get; set; } = new();
        public List<int> MonthlyBorrowStats { get; set; } = new();

        public List<int> MonthlyMemberStats { get; set; } = new(); 
        public List<string> DailyLabels { get; set; } = new();
        public List<int> DailyBorrowStats { get; set; } = new();

        // ===== Compatibility aliases (សម្រាប់ code ចាស់) =====
        public int TotalLoans
        {
            get => TotalBorrows;
            set => TotalBorrows = value;
        }
        public int OverdueLoans
        {
            get => OverdueBorrows;
            set => OverdueBorrows = value;
        }
        public int ReturnedBooks
        {
            get => ReturnedBorrows;
            set => ReturnedBorrows = value;
        }
        public List<int> MonthlyLoanStats
        {
            get => MonthlyBorrowStats;
            set => MonthlyBorrowStats = value ?? new List<int>();
        }
        public List<int> DailyLoanStats
        {
            get => DailyBorrowStats;
            set => DailyBorrowStats = value ?? new List<int>();
        }

        public int TotalBookTypes { get; set; }
    }
}
