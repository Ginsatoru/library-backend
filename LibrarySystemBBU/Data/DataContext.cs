using Microsoft.EntityFrameworkCore;
using LibrarySystemBBU.Models;
using System;

namespace LibrarySystemBBU.Data
{
    public sealed class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- Users ---
            modelBuilder.Entity<Users>()
                .Property(p => p.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Users>()
                .Property(p => p.Created)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Users>()
                .Property(p => p.Modified)
                .HasDefaultValueSql("GETDATE()")
                .ValueGeneratedOnAddOrUpdate();

            // --- Roles ---
            modelBuilder.Entity<Roles>()
                .Property(r => r.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Roles>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(256);

            // --- Permissions ---
            modelBuilder.Entity<Permission>()
                .Property(p => p.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Permission>()
                .Property(p => p.ClaimName)
                .IsRequired()
                .HasMaxLength(100);

            modelBuilder.Entity<Permission>()
                .Property(p => p.ClaimValue)
                .IsRequired()
                .HasMaxLength(100);

            // --- LibraryMember ---
            modelBuilder.Entity<Member>()
                .Property(lm => lm.MemberId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Member>()
                .Property(lm => lm.JoinDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Member>()
                .Property(lm => lm.Modified)
                .HasDefaultValueSql("GETDATE()")
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<Member>()
                .HasOne(lm => lm.Users)
                .WithMany()
                .HasForeignKey(lm => lm.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Book ---
            modelBuilder.Entity<Book>()
                .Property(lb => lb.BookId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Book>()
                .HasMany(b => b.LoanBookDetails)
                .WithOne(ld => ld.Book)
                .HasForeignKey(ld => ld.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasMany(b => b.Purchases)
                .WithOne(pu => pu.Book)
                .HasForeignKey(pu => pu.BookId) // CORRECT foreign key!
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasMany(b => b.Catalogs)
                .WithOne(c => c.Book)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- BookLoan ---
            modelBuilder.Entity<BookLoan>()
                .Property(bl => bl.LoanId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<BookLoan>()
                .Property(bl => bl.LoanDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<BookLoan>()
                .Property(bl => bl.IsReturned)
                .HasDefaultValue(false);

            modelBuilder.Entity<BookLoan>()
                .HasOne(bl => bl.LibraryMember)
                .WithMany(lm => lm.BookLoans)
                .HasForeignKey(bl => bl.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookLoan>()
                .HasMany(bl => bl.LoanBookDetails)
                .WithOne(ld => ld.Loan)
                .HasForeignKey(ld => ld.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- BookReturn ---
            modelBuilder.Entity<BookReturn>()
                .Property(br => br.ReturnId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<BookReturn>()
                .Property(br => br.ReturnDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<BookReturn>()
                .Property(br => br.FineAmount)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<BookReturn>()
                .Property(br => br.AmountPaid)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<BookReturn>()
                .Property(br => br.RefundAmount)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<BookReturn>()
                .HasOne(br => br.Loan)
                .WithMany(bl => bl.BookReturns)
                .HasForeignKey(br => br.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- LoanReminder ---
            modelBuilder.Entity<LoanReminder>()
                .Property(lr => lr.ReminderId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<LoanReminder>()
                .Property(lr => lr.SentDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<LoanReminder>()
                .HasOne(lr => lr.Loan)
                .WithMany(bl => bl.LoanReminders)
                .HasForeignKey(lr => lr.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- Catalog ---
            modelBuilder.Entity<Catalog>()
                .Property(c => c.CatalogId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Catalog>()
                .Property(c => c.AcquisitionDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Catalog>()
                .Property(c => c.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Catalog>()
                .Property(c => c.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<Catalog>()
                .HasOne(c => c.Book)
                .WithMany(b => b.Catalogs)
                .HasForeignKey(c => c.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- Adjustment ---
            modelBuilder.Entity<Adjustment>()
                .Property(a => a.AdjustmentId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Adjustment>()
                .Property(a => a.AdjustmentDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Adjustment>()
                .Property(a => a.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Adjustment>()
                .Property(a => a.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<Adjustment>()
                .HasOne(a => a.Catalog)
                .WithMany()
                .HasForeignKey(a => a.CatalogId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Adjustment>()
                .HasOne(a => a.AdjustedByUser)
                .WithMany()
                .HasForeignKey(a => a.AdjustedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            // --- Purchase ---
            modelBuilder.Entity<Purchase>()
                .Property(pu => pu.PurchaseId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Purchase>()
                .Property(pu => pu.PurchaseDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<Purchase>()
                .Property(pu => pu.Cost)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<Purchase>()
                .Property(pu => pu.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<Purchase>()
                .Property(pu => pu.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            // CORRECT: FK is BookId, not BookTitle!
            modelBuilder.Entity<Purchase>()
                .HasOne(pu => pu.Book)
                .WithMany(b => b.Purchases)
                .HasForeignKey(pu => pu.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // --- LoanBookDetail ---
            modelBuilder.Entity<LoanBookDetail>()
                .Property(ld => ld.FineDetailAmount)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<LoanBookDetail>()
                .Property(ld => ld.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<LoanBookDetail>()
                .Property(ld => ld.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            // --- PurchaseDetail ---
            modelBuilder.Entity<PurchaseDetail>()
                .Property(pd => pd.UnitPrice)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<PurchaseDetail>()
                .Property(pd => pd.LineTotal)
                .HasColumnType("decimal(18, 2)");

            modelBuilder.Entity<PurchaseDetail>()
                .Property(pd => pd.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<PurchaseDetail>()
                .Property(pd => pd.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            modelBuilder.Entity<PurchaseDetail>()
                .HasOne(pd => pd.Purchase)
                .WithMany(p => p.PurchaseDetails)
                .HasForeignKey(pd => pd.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // --- AdjustmentDetail ---
            modelBuilder.Entity<AdjustmentDetail>()
                .Property(ad => ad.QuantityChanged)
                .IsRequired();

            modelBuilder.Entity<AdjustmentDetail>()
                .HasOne(ad => ad.Adjustment)
                .WithMany(a => a.AdjustmentDetails)
                .HasForeignKey(ad => ad.AdjustmentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AdjustmentDetail>()
                .HasOne(ad => ad.Catalog)
                .WithMany()
                .HasForeignKey(ad => ad.CatalogId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }

        // DbSets
        public DbSet<Users> Users { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<Member> Members { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookLoan> BookLoans { get; set; }
        public DbSet<BookReturn> BookReturns { get; set; }
        public DbSet<LoanReminder> LoanReminders { get; set; }
        public DbSet<Catalog> Catalogs { get; set; }
        public DbSet<Adjustment> Adjustments { get; set; }
        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<LoanBookDetail> LoanBookDetails { get; set; }
        public DbSet<PurchaseDetail> PurchaseDetails { get; set; }
        public DbSet<AdjustmentDetail> AdjustmentDetails { get; set; }
    }
}
