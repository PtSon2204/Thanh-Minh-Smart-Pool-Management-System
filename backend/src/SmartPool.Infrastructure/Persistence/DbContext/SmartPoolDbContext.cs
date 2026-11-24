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

    public virtual DbSet<Employee> Employees { get; set; } //1
    public DbSet<StaffInvitation> StaffInvitations { get; set; } //2
    public DbSet<StaffOnboardingRevision> StaffOnboardingRevisions { get; set; } //3
    public DbSet<StaffOnboardingDocument> StaffOnboardingDocuments { get; set; }  //4
    public DbSet<StaffScheduleChange> StaffScheduleChanges { get; set; }  //5
    public DbSet<StaffScheduleDelivery> StaffScheduleDeliveries { get; set; }  //6 
    public virtual DbSet<EmployeeShift> EmployeeShifts { get; set; } //7
    public virtual DbSet<AttendanceLog> AttendanceLogs { get; set; } //8
    public virtual DbSet<EntryLog> EntryLogs { get; set; }  //9
    public virtual DbSet<Equipment> Equipments { get; set; }  //10
    public virtual DbSet<Incident> Incidents { get; set; } //11
    public virtual DbSet<InventoryLog> InventoryLogs { get; set; } //12
    public virtual DbSet<DailyInspection> DailyInspections { get; set; } //13
    public virtual DbSet<Notification> Notifications { get; set; } //14
    public virtual DbSet<Order> Orders { get; set; } //15
    public virtual DbSet<OrderDetail> OrderDetails { get; set; } //16
    public virtual DbSet<Payment> Payments { get; set; } //17
    public virtual DbSet<Product> Products { get; set; } //18
    public virtual DbSet<Rental> Rentals { get; set; }  //19
    public virtual DbSet<RentalTransaction> RentalTransactions { get; set; } //20
    public virtual DbSet<RentalPaymentIntent> RentalPaymentIntents { get; set; } //21
    public virtual DbSet<BankTransferReceipt> BankTransferReceipts { get; set; } //22
    public virtual DbSet<Role> Roles { get; set; } //23
    public virtual DbSet<Shift> Shifts { get; set; }  //24
    public virtual DbSet<Ticket> Tickets { get; set; } //25
    public virtual DbSet<TicketType> TicketTypes { get; set; } //26
    public virtual DbSet<User> Users { get; set; } //27
    public virtual DbSet<UserProfile> UserProfiles { get; set; } //28
    public virtual DbSet<Voucher> Vouchers { get; set; } //29
    public DbSet<Warning> Warnings { get; set; } //30
    public DbSet<RefreshToken> RefreshTokens { get; set; } //31
    public DbSet<WaterQualityLog> WaterQualityLogs { get; set; } //32
    public DbSet<PoolDutySchedule> PoolDutySchedules { get; set; }  //33
    public DbSet<PoolDutyMeasurementRecurrence> PoolDutyMeasurementRecurrences { get; set; } //34
    public DbSet<SwimClass> SwimClasses { get; set; }  //35
    public DbSet<ClassSession> ClassSessions { get; set; }  //36
    public DbSet<ClassRegistration> ClassRegistrations { get; set; } //37
    public DbSet<ClassAttendance> ClassAttendances { get; set; } //38
    public DbSet<CoachApplication> CoachApplications { get; set; } //39
    public DbSet<PoolOperationalConfig> PoolOperationalConfigs { get; set; } //40
    public DbSet<SafetyMonitoringThreshold> SafetyMonitoringThresholds { get; set; } //41

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto").HasPostgresExtension("smart_pool", "uuid-ossp");
        modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
        var scheduling = new StaffScheduleChangeConfiguration();
        modelBuilder.ApplyConfiguration<StaffScheduleChange>(scheduling);
        modelBuilder.ApplyConfiguration<StaffScheduleDelivery>(scheduling);
        var onboarding = new StaffOnboardingConfiguration();
        modelBuilder.ApplyConfiguration<StaffInvitation>(onboarding);
        modelBuilder.ApplyConfiguration<StaffOnboardingRevision>(onboarding);
        modelBuilder.ApplyConfiguration<StaffOnboardingDocument>(onboarding);
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
        modelBuilder.ApplyConfiguration(new PoolDutyScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new PoolDutyMeasurementRecurrenceConfiguration());
        modelBuilder.ApplyConfiguration(new SwimClassConfiguration());
        modelBuilder.ApplyConfiguration(new ClassSessionConfiguration());
        modelBuilder.ApplyConfiguration(new ClassRegistrationConfiguration());
        modelBuilder.ApplyConfiguration(new ClassAttendanceConfiguration());
        modelBuilder.ApplyConfiguration(new CoachApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new PoolOperationalConfigConfiguration());
        modelBuilder.ApplyConfiguration(new SafetyMonitoringThresholdConfiguration());
    }
}
