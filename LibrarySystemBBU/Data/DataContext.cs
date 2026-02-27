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
            // ---------- Users ----------
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

            // ---------- Roles ----------
            modelBuilder.Entity<Roles>()
                .Property(r => r.Id)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Roles>()
                .Property(r => r.Name)
                .IsRequired()
                .HasMaxLength(256);

            // ---------- Permissions ----------
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

            // ---------- Member ----------
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

            // ========================
            //         BOOK
            // ========================
            modelBuilder.Entity<Book>()
                .Property(b => b.BookId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Book>()
                .HasIndex(b => b.Barcode)
                .IsUnique();

            modelBuilder.Entity<Book>()
                .HasOne(b => b.Catalog)
                .WithMany(c => c.Books)
                .HasForeignKey(b => b.CatalogId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Book>()
                .HasMany(b => b.PurchaseDetails)
                .WithOne(pd => pd.Book)
                .HasForeignKey(pd => pd.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity(BookBorrowDetailEntity());

            // ========================
            //       BOOK BORROW
            // ========================
            modelBuilder.Entity<BookBorrow>()
                .Property(bl => bl.LoanId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<BookBorrow>()
                .Property(bl => bl.LoanDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<BookBorrow>()
                .Property(bl => bl.IsReturned)
                .HasDefaultValue(false);

            modelBuilder.Entity<BookBorrow>()
                .HasOne(bl => bl.LibraryMember)
                .WithMany(lm => lm.BookLoans)
                .HasForeignKey(bl => bl.MemberId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookBorrow>()
                .HasMany(bl => bl.LoanBookDetails)
                .WithOne(ld => ld.Loan)
                .HasForeignKey(ld => ld.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookBorrowDetail>()
                .Property(ld => ld.FineDetailAmount)
                .HasColumnType("decimal(10, 2)");

            modelBuilder.Entity<BookBorrowDetail>()
                .Property(ld => ld.Created)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<BookBorrowDetail>()
                .Property(ld => ld.Modified)
                .HasDefaultValueSql("GETUTCDATE()")
                .ValueGeneratedOnAddOrUpdate();

            // ========================
            //       BOOK RETURN
            // ========================
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

            // ========================
            //       LOAN REMINDER
            // ========================
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

            // ========================
            //         CATALOG
            // ========================
            modelBuilder.Entity<Catalog>()
                .Property(c => c.CatalogId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<Catalog>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Catalog_TotalCopies_NonNegative", "[TotalCopies] >= 0");
                    t.HasCheckConstraint("CK_Catalog_AvailableCopies_NonNegative", "[AvailableCopies] >= 0");
                    t.HasCheckConstraint("CK_Catalog_Available_LE_Total", "[AvailableCopies] <= [TotalCopies]");
                });

            // ========================
            //        PURCHASE
            // ========================
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

            modelBuilder.Entity<Purchase>()
                .HasOne(pu => pu.Book)
                .WithMany()
                .HasForeignKey(pu => pu.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Purchase>()
                .HasMany(pu => pu.PurchaseDetails)
                .WithOne(pd => pd.Purchase)
                .HasForeignKey(pd => pd.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            // ========================
            //     PURCHASE DETAIL
            // ========================
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
                .HasOne(pd => pd.Book)
                .WithMany(b => b.PurchaseDetails)
                .HasForeignKey(pd => pd.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // ========================
            //       ADJUSTMENT
            // ========================
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

            // ========================
            //       LIBRARY LOG
            // ========================
            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.LogId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.StudentName)
                .IsRequired()
                .HasMaxLength(150);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.PhoneNumber)
                .HasMaxLength(20);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.Gender)
                .HasMaxLength(10);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.Purpose)
                .HasMaxLength(200);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.Notes)
                .HasMaxLength(255);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.VisitDate)
                .HasDefaultValueSql("GETDATE()");

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.CreatedUtc)
                .HasDefaultValueSql("GETUTCDATE()");

            // NEW: workflow fields
            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending")
                .IsRequired();

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.ApprovedUtc)
                .IsRequired(false);

            modelBuilder.Entity<LibraryLog>()
                .Property(l => l.ReturnedUtc)
                .IsRequired(false);

            // Helpful indexes
            modelBuilder.Entity<LibraryLog>()
                .HasIndex(l => l.VisitDate);

            modelBuilder.Entity<LibraryLog>()
                .HasIndex(l => l.Status);

            // Items (1..N)
            modelBuilder.Entity<LibraryLogItem>()
                .Property(i => i.LogItemId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<LibraryLogItem>()
                .HasOne(i => i.Log)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.LogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<LibraryLogItem>()
                .HasOne(i => i.Book)
                .WithMany()
                .HasForeignKey(i => i.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: per-item return time (optional)
            modelBuilder.Entity<LibraryLogItem>()
                .Property(i => i.ReturnedDate)
                .IsRequired(false);

            // Prevent duplicate same book in one log
            modelBuilder.Entity<LibraryLogItem>()
                .HasIndex(i => new { i.LogId, i.BookId })
                .IsUnique();

            // ========================
            //     MEMBER WISHLIST
            // ========================
            modelBuilder.Entity<MemberWishlist>()
                .Property(w => w.WishlistId)
                .ValueGeneratedOnAdd();

            modelBuilder.Entity<MemberWishlist>()
                .HasOne(w => w.Member)
                .WithMany()
                .HasForeignKey(w => w.MemberId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemberWishlist>()
                .HasOne(w => w.Catalog)
                .WithMany()
                .HasForeignKey(w => w.CatalogId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MemberWishlist>()
                .HasIndex(w => new { w.MemberId, w.CatalogId })
                .IsUnique();

            base.OnModelCreating(modelBuilder);
        }

        // helper config for BookBorrowDetail
        private static Action<Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Book>> BookBorrowDetailEntity()
            => _ => { };

        // ----------------- DbSets -----------------
        public DbSet<Users> Users { get; set; } = null!;
        public DbSet<Roles> Roles { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<Member> Members { get; set; } = null!;
        public DbSet<Book> Books { get; set; } = null!;
        public DbSet<Catalog> Catalogs { get; set; } = null!;
        public DbSet<BookBorrow> BookBorrows { get; set; } = null!;
        public DbSet<BookBorrowDetail> LoanBookDetails { get; set; } = null!;
        public DbSet<BookReturn> BookReturns { get; set; } = null!;
        public DbSet<LoanReminder> LoanReminders { get; set; } = null!;
        public DbSet<Adjustment> Adjustments { get; set; } = null!;
        public DbSet<AdjustmentDetail> AdjustmentDetails { get; set; } = null!;
        public DbSet<Purchase> Purchases { get; set; } = null!;
        public DbSet<PurchaseDetail> PurchaseDetails { get; set; } = null!;
        public DbSet<LibraryLog> LibraryLogs { get; set; } = null!;
        public DbSet<LibraryLogItem> LibraryLogItems { get; set; } = null!;
        public DbSet<History> Histories { get; set; } = null!;
        public DbSet<MemberWishlist> MemberWishlists { get; set; } = null!;
    }
}
