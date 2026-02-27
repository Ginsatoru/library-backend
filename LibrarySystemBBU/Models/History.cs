namespace LibrarySystemBBU.Models
{
    public class History
    {

        public long Id { get; set; }

       
        public string EntityType { get; set; } = null!;
        public string ActionType { get; set; } = null!;

        // Main references
        public int? LoanId { get; set; }
        public int? LogId { get; set; }
        public int? MemberId { get; set; } 
        public Guid? CatalogId { get; set; } 
        public int? BookId { get; set; } 

        // Snapshot info (denormalized for fast history view)
        public string? MemberName { get; set; }
        public string? BookTitle { get; set; }
        public string? CatalogTitle { get; set; }

        public int? Quantity { get; set; }         

        // Dates / lifecycle
        public DateTime OccurredUtc { get; set; } 
        public DateTime? LoanDate { get; set; }  
        public DateTime? DueDate { get; set; }
        public DateTime? ReturnDate { get; set; }

        // Money info (optional)
        public decimal? BorrowingFee { get; set; }
        public decimal? FineAmount { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal? DepositAmount { get; set; }


        // Other
        public string? LocationType { get; set; } 
        public string? Notes { get; set; }
    }
}
