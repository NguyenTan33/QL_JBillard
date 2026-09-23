using System.IO;
using Microsoft.EntityFrameworkCore;
using QL_JBillard.Domain.Entities;

namespace QL_JBillard.Data;

public class AppDbContext : DbContext
{
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<TableType> TableTypes => Set<TableType>();
    public DbSet<TimeSlotPricing> TimeSlotPricings => Set<TimeSlotPricing>();
    public DbSet<PlayCombo> PlayCombos => Set<PlayCombo>();
    public DbSet<Table> Tables => Set<Table>();
    public DbSet<User> Users => Set<User>();
    public DbSet<MembershipTier> MembershipTiers => Set<MembershipTier>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
    public DbSet<StockTransactionDetail> StockTransactionDetails => Set<StockTransactionDetail>();
    public DbSet<TableSession> TableSessions => Set<TableSession>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<BankDepositSlip> BankDepositSlips => Set<BankDepositSlip>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DatabaseBackupLog> DatabaseBackupLogs => Set<DatabaseBackupLog>();

    public string DbPath { get; }

    public static string DefaultDbPath
    {
        get
        {
            var appFolder = AppDomain.CurrentDomain.BaseDirectory;
            var dataFolder = Path.Combine(appFolder, "AppData");
            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
            }
            return Path.Combine(dataFolder, "qlin_billiard.db");
        }
    }

    public AppDbContext()
    {
        DbPath = DefaultDbPath;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
        DbPath = DefaultDbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={DbPath}");
            optionsBuilder.EnableSensitiveDataLogging(false);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- CẤU HÌNH KHÓA CHÍNH VÀ RÀNG BUỘC CHUẨN HÓA 3NF ---

        // 1. Floor
        modelBuilder.Entity<Floor>(entity =>
        {
            entity.HasKey(e => e.FloorId);
            entity.Property(e => e.FloorName).IsRequired().HasMaxLength(100);
        });

        // 2. Area
        modelBuilder.Entity<Area>(entity =>
        {
            entity.HasKey(e => e.AreaId);
            entity.Property(e => e.AreaName).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Floor)
                  .WithMany(f => f.Areas)
                  .HasForeignKey(e => e.FloorId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 3. TableType
        modelBuilder.Entity<TableType>(entity =>
        {
            entity.HasKey(e => e.TableTypeId);
            entity.Property(e => e.TypeName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DefaultHourlyRate).HasColumnType("decimal(18,2)");
        });

        // 3b. TimeSlotPricing
        modelBuilder.Entity<TimeSlotPricing>(entity =>
        {
            entity.HasKey(e => e.TimeSlotId);
            entity.Property(e => e.SlotName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.TableType)
                  .WithMany()
                  .HasForeignKey(e => e.TableTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3c. PlayCombo
        modelBuilder.Entity<PlayCombo>(entity =>
        {
            entity.HasKey(e => e.ComboId);
            entity.Property(e => e.ComboName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.TableType)
                  .WithMany()
                  .HasForeignKey(e => e.TableTypeId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 4. Table
        modelBuilder.Entity<Table>(entity =>
        {
            entity.HasKey(e => e.TableId);
            entity.Property(e => e.TableNumber).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Area)
                  .WithMany(a => a.Tables)
                  .HasForeignKey(e => e.AreaId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.TableType)
                  .WithMany(t => t.Tables)
                  .HasForeignKey(e => e.TableTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CurrentSession)
                  .WithMany()
                  .HasForeignKey(e => e.CurrentSessionId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 5. User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Salt).IsRequired();
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
        });

        // 6. MembershipTier
        modelBuilder.Entity<MembershipTier>(entity =>
        {
            entity.HasKey(e => e.TierId);
            entity.Property(e => e.TierName).IsRequired().HasMaxLength(50);
        });

        // 7. Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.Property(e => e.PhoneNumber).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.PhoneNumber).IsUnique(); // Index tìm kiếm cực nhanh qua SĐT
            entity.Property(e => e.FullName).IsRequired().HasMaxLength(100);
            entity.HasOne(e => e.Tier)
                  .WithMany(t => t.Customers)
                  .HasForeignKey(e => e.TierId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 8. ProductCategory & Unit
        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.UnitId);
            entity.Property(e => e.UnitName).IsRequired().HasMaxLength(50);
        });

        // 9. Product & Inventory
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId);
            entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(150);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Unit)
                  .WithMany(u => u.Products)
                  .HasForeignKey(e => e.UnitId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.InventoryItemId);
            entity.HasIndex(e => e.ProductId).IsUnique();
            entity.HasOne(e => e.Product)
                  .WithOne(p => p.InventoryItem)
                  .HasForeignKey<InventoryItem>(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 10. TableSession
        modelBuilder.Entity<TableSession>(entity =>
        {
            entity.HasKey(e => e.SessionId);
            entity.HasOne(e => e.Table)
                  .WithMany(t => t.Sessions)
                  .HasForeignKey(e => e.TableId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Sessions)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.CreatedByUser)
                  .WithMany(u => u.CreatedSessions)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 11. OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.OrderItemId);
            entity.HasOne(e => e.Session)
                  .WithMany(s => s.OrderItems)
                  .HasForeignKey(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 12. Invoice & InvoicePayment
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId);
            entity.Property(e => e.InvoiceCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.InvoiceCode).IsUnique();
            entity.HasOne(e => e.Session)
                  .WithOne(s => s.Invoice)
                  .HasForeignKey<Invoice>(e => e.SessionId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CashierUser)
                  .WithMany(u => u.Invoices)
                  .HasForeignKey(e => e.CashierUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoicePayment>(entity =>
        {
            entity.HasKey(e => e.PaymentId);
            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.Payments)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 13. SystemSetting (Key-Value)
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingKey);
            entity.Property(e => e.SettingKey).HasMaxLength(100);
        });

        // 14. AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.HasIndex(e => e.CreatedAt);
        });

        // 15. StockTransaction & Details
        modelBuilder.Entity<StockTransaction>(entity =>
        {
            entity.HasKey(e => e.StockTransactionId);
            entity.HasOne(e => e.PerformedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.PerformedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockTransactionDetail>(entity =>
        {
            entity.HasKey(e => e.DetailId);
            entity.HasOne(e => e.StockTransaction)
                  .WithMany(s => s.Details)
                  .HasForeignKey(e => e.StockTransactionId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                  .WithMany(p => p.StockTransactionDetails)
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 16. Shift
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.ShiftId);
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Shifts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 17. BankDepositSlip
        modelBuilder.Entity<BankDepositSlip>(entity =>
        {
            entity.HasKey(e => e.DepositSlipId);
            entity.Property(e => e.SlipCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.HasOne(e => e.User)
                  .WithMany(u => u.BankDepositSlips)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Shift)
                  .WithMany(s => s.BankDepositSlips)
                  .HasForeignKey(e => e.ShiftId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ApprovedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ApprovedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 18. DatabaseBackupLog
        modelBuilder.Entity<DatabaseBackupLog>(entity =>
        {
            entity.HasKey(e => e.BackupLogId);
        });
    }
}
