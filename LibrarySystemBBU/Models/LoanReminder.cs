using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LibrarySystemBBU.Models
{

    public class LoanReminder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReminderId { get; set; }

        [Required(ErrorMessage = "Loan ID is required.")]
        public int LoanId { get; set; }

        /// <summary>
        /// When this reminder was sent.
        /// </summary>
        [DataType(DataType.DateTime)]
        public DateTime SentDate { get; set; }

        [Required(ErrorMessage = "Reminder Type is required.")]
        [StringLength(50)]
        public string ReminderType { get; set; } = string.Empty;

        [ForeignKey(nameof(LoanId))]
        public BookBorrow? Loan { get; set; }
    }
}
