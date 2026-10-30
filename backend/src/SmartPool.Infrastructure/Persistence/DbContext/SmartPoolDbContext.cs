using Microsoft.EntityFrameworkCore;
using SmartPool.Domain.Entities;
using SmartPool.Infrastructure.Persistence.Configurations;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace SmartPool.Infrastructure.Persistence.DbContext;

public class SmartPoolDbContext : EfDbContext
{
    public SmartPoolDbContext()
    {
    }

    public SmartPoolDbContext(DbContextOptions<SmartPoolDbContext> options) : base(options)
    {
    }

    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<EmployeeShift> EmployeeShifts { get; set; }
    public virtual DbSet<AttendanceLog> AttendanceLogs { get; set; }
    public virtual DbSet<EntryLog> EntryLogs { get; set; }
    public virtual DbSet<Equipment> Equipments { get; set; }
    public virtual DbSet<Incident> Incidents { get; set; }
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }
    public virtual DbSet<DailyInspection> DailyInspections { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<Order> Orders { get; set; }
    public virtual DbSet<OrderDetail> OrderDetails { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<Rental> Rentals { get; set; }
    public virtual DbSet<RentalTransaction> RentalTransactions { get; set; }
    public virtual DbSet<RentalPaymentIntent> RentalPaymentIntents { get; set; }
    public virtual DbSet<BankTransferReceipt> BankTransferReceipts { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Shift> Shifts { get; set; }
    public virtual DbSet<Ticket> Tickets { get; set; }
    public virtual DbSet<TicketType> TicketTypes { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserProfile> UserProfiles { get; set; }
    public virtual DbSet<Voucher> Vouchers { get; set; }
    public DbSet<Warning> Warnings { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<WaterQualityLog> WaterQualityLogs { get; set; }
    public DbSet<SwimClass> SwimClasses { get; set; }
    public DbSet<ClassSession> ClassSessions { get; set; }
    public DbSet<ClassRegistration> ClassRegistrations { get; set; }
    public DbSet<ClassAttendance> ClassAttendances { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto").HasPostgresExtension("smart_pool", "uuid-ossp");
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeShiftConfiguration());
        modelBuilder.ApplyConfiguration(new AttendanceLogConfiguration());
        modelBuilder.ApplyConfiguration(new EntryLogConfiguration());
        modelBuilder.ApplyConfiguration(new EquipmentConfiguration());
        modelBuilder.ApplyConfiguration(new IncidentConfiguration());
        modelBuilder.ApplyConfiguration(new InventoryLogConfiguration());
        modelBuilder.ApplyConfiguration(new DailyInspectionConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderDetailConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new RentalConfiguration());
        modelBuilder.ApplyConfiguration(new RentalTransactionConfiguration());
        modelBuilder.ApplyConfiguration(new RentalPaymentIntentConfiguration());
        modelBuilder.ApplyConfiguration(new BankTransferReceiptConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new ShiftConfiguration());
        modelBuilder.ApplyConfiguration(new TicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketTypeConfiguration());
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
        modelBuilder.ApplyConfiguration(new VoucherConfiguration());
        modelBuilder.HasDefaultSchema("smart_pool");
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new WarningConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new WaterQualityLogConfiguration());
        modelBuilder.ApplyConfiguration(new SwimClassConfiguration());
        modelBuilder.ApplyConfiguration(new ClassSessionConfiguration());
        modelBuilder.ApplyConfiguration(new ClassRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new ClassAttendanceConfiguration());
    }
}
