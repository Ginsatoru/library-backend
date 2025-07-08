using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{
    // Represents a reminder sent for a book loan (based on the 'LoanReminders' table in your ERD).
    public class LoanReminder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReminderId { get; set; } // Primary Key (INT as per ERD)

        // Foreign Key to BookLoans
        [Required(ErrorMessage = "Loan ID is required.")]
        public int LoanId { get; set; }

        [Required(ErrorMessage = "Sent Date is required.")]
        [DataType(DataType.Date)]
        public DateTime SentDate { get; set; }

        [Required(ErrorMessage = "Reminder Type is required.")]
        [StringLength(50)]
        public required string ReminderType { get; set; }

        // Navigation property
        [ForeignKey("LoanId")]
        public BookLoan? Loan { get; set; }
    }
}
