using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SmartPool.Infrastructure.Persistence.TempModels;
using EfDbContext = Microsoft.EntityFrameworkCore.DbContext;

namespace SmartPool.Infrastructure.Persistence.DbContext;

public partial class SmartPoolDbContext : EfDbContext
{
    public SmartPoolDbContext()
    {
    }

    public SmartPoolDbContext(DbContextOptions<SmartPoolDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Employee> Employees { get; set; }

    public virtual DbSet<EmployeeShift> EmployeeShifts { get; set; }

    public virtual DbSet<EntryLog> EntryLogs { get; set; }

    public virtual DbSet<Equipment> Equipments { get; set; }

    public virtual DbSet<Incident> Incidents { get; set; }

    public virtual DbSet<InventoryLog> InventoryLogs { get; set; }

    public virtual DbSet<MaintenanceLog> MaintenanceLogs { get; set; }

    public virtual DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Rental> Rentals { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Salary> Salaries { get; set; }

    public virtual DbSet<Shift> Shifts { get; set; }

    public virtual DbSet<Ticket> Tickets { get; set; }

    public virtual DbSet<TicketType> TicketTypes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresExtension("pgcrypto")
            .HasPostgresExtension("smart_pool", "uuid-ossp");

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("employees_pkey");

            entity.ToTable("employees", "smart_pool");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.BaseSalary)
                .HasPrecision(18, 2)
                .HasColumnName("base_salary");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.JoinDate).HasColumnName("join_date");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Working'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.Employee)
                .HasForeignKey<Employee>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("employees_user_id_fkey");
        });

        modelBuilder.Entity<EmployeeShift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("employee_shifts_pkey");

            entity.ToTable("employee_shifts", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CheckInTime).HasColumnName("check_in_time");
            entity.Property(e => e.CheckOutTime).HasColumnName("check_out_time");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Scheduled'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.WorkDate).HasColumnName("work_date");

            entity.HasOne(d => d.Employee).WithMany(p => p.EmployeeShifts)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("employee_shifts_employee_id_fkey");

            entity.HasOne(d => d.Shift).WithMany(p => p.EmployeeShifts)
                .HasForeignKey(d => d.ShiftId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("employee_shifts_shift_id_fkey");
        });

        modelBuilder.Entity<EntryLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("entry_logs_pkey");

            entity.ToTable("entry_logs", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.GateId)
                .HasMaxLength(50)
                .HasColumnName("gate_id");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.ScanTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("scan_time");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status");
            entity.Property(e => e.TicketId).HasColumnName("ticket_id");

            entity.HasOne(d => d.Ticket).WithMany(p => p.EntryLogs)
                .HasForeignKey(d => d.TicketId)
                .HasConstraintName("entry_logs_ticket_id_fkey");
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("equipments_pkey");

            entity.ToTable("equipments", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Location)
                .HasMaxLength(255)
                .HasColumnName("location");
            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasColumnName("metadata");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Incident>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("incidents_pkey");

            entity.ToTable("incidents", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IncidentTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("incident_time");
            entity.Property(e => e.ReportedBy).HasColumnName("reported_by");
            entity.Property(e => e.SeverityLevel)
                .HasMaxLength(50)
                .HasColumnName("severity_level");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.Incidents)
                .HasForeignKey(d => d.ReportedBy)
                .HasConstraintName("incidents_reported_by_fkey");
        });

        modelBuilder.Entity<InventoryLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("inventory_logs_pkey");

            entity.ToTable("inventory_logs", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.ChangeType)
                .HasMaxLength(50)
                .HasColumnName("change_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CreatedBy).HasColumnName("created_by");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.InventoryLogs)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("inventory_logs_created_by_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.InventoryLogs)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("inventory_logs_product_id_fkey");
        });

        modelBuilder.Entity<MaintenanceLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("maintenance_logs_pkey");

            entity.ToTable("maintenance_logs", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.ActualDate).HasColumnName("actual_date");
            entity.Property(e => e.Cost)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("cost");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.Note).HasColumnName("note");
            entity.Property(e => e.PerformedBy).HasColumnName("performed_by");
            entity.Property(e => e.ScheduleId).HasColumnName("schedule_id");
            entity.Property(e => e.VendorName)
                .HasMaxLength(255)
                .HasColumnName("vendor_name");

            entity.HasOne(d => d.Equipment).WithMany(p => p.MaintenanceLogs)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("maintenance_logs_equipment_id_fkey");

            entity.HasOne(d => d.PerformedByNavigation).WithMany(p => p.MaintenanceLogs)
                .HasForeignKey(d => d.PerformedBy)
                .HasConstraintName("maintenance_logs_performed_by_fkey");

            entity.HasOne(d => d.Schedule).WithMany(p => p.MaintenanceLogs)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("maintenance_logs_schedule_id_fkey");
        });

        modelBuilder.Entity<MaintenanceSchedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("maintenance_schedules_pkey");

            entity.ToTable("maintenance_schedules", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EquipmentId).HasColumnName("equipment_id");
            entity.Property(e => e.PlannedDate).HasColumnName("planned_date");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.MaintenanceSchedules)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("maintenance_schedules_assigned_to_fkey");

            entity.HasOne(d => d.Equipment).WithMany(p => p.MaintenanceSchedules)
                .HasForeignKey(d => d.EquipmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("maintenance_schedules_equipment_id_fkey");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("notifications_pkey");

            entity.ToTable("notifications", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notifications_user_id_fkey");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("orders_pkey");

            entity.ToTable("orders", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(255)
                .HasColumnName("customer_name");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20)
                .HasColumnName("customer_phone");
            entity.Property(e => e.DiscountAmount)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("discount_amount");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.FinalAmount)
                .HasPrecision(18, 2)
                .HasColumnName("final_amount");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Pending'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(18, 2)
                .HasColumnName("total_amount");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VoucherId).HasColumnName("voucher_id");

            entity.HasOne(d => d.Employee).WithMany(p => p.Orders)
                .HasForeignKey(d => d.EmployeeId)
                .HasConstraintName("orders_employee_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("orders_user_id_fkey");

            entity.HasOne(d => d.Voucher).WithMany(p => p.Orders)
                .HasForeignKey(d => d.VoucherId)
                .HasConstraintName("orders_voucher_id_fkey");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("order_details_pkey");

            entity.ToTable("order_details", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ItemId).HasColumnName("item_id");
            entity.Property(e => e.ItemType)
                .HasMaxLength(50)
                .HasColumnName("item_type");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.UnitPrice)
                .HasPrecision(18, 2)
                .HasColumnName("unit_price");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("order_details_order_id_fkey");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("payments_pkey");

            entity.ToTable("payments", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Amount)
                .HasPrecision(18, 2)
                .HasColumnName("amount");
            entity.Property(e => e.CashierId).HasColumnName("cashier_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50)
                .HasColumnName("payment_method");
            entity.Property(e => e.PaymentTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("payment_time");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Completed'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TransactionRef)
                .HasMaxLength(255)
                .HasColumnName("transaction_ref");

            entity.HasOne(d => d.Cashier).WithMany(p => p.Payments)
                .HasForeignKey(d => d.CashierId)
                .HasConstraintName("payments_cashier_id_fkey");

            entity.HasOne(d => d.Order).WithMany(p => p.Payments)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_order_id_fkey");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("products_pkey");

            entity.ToTable("products", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .HasColumnName("price");
            entity.Property(e => e.RowVersion)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("row_version");
            entity.Property(e => e.StockQuantity)
                .HasDefaultValue(0)
                .HasColumnName("stock_quantity");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("rentals_pkey");

            entity.ToTable("rentals", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.DepositAmount)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("deposit_amount");
            entity.Property(e => e.OrderId).HasColumnName("order_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.RentTime)
                .HasDefaultValueSql("now()")
                .HasColumnName("rent_time");
            entity.Property(e => e.ReturnTime).HasColumnName("return_time");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Renting'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.Order).WithMany(p => p.Rentals)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("rentals_order_id_fkey");

            entity.HasOne(d => d.Product).WithMany(p => p.Rentals)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("rentals_product_id_fkey");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("roles_pkey");

            entity.ToTable("roles", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Permissions)
                .HasColumnType("jsonb")
                .HasColumnName("permissions");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Salary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("salaries_pkey");

            entity.ToTable("salaries", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Bonus)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("bonus");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Deduction)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("deduction");
            entity.Property(e => e.EmployeeId).HasColumnName("employee_id");
            entity.Property(e => e.Month).HasColumnName("month");
            entity.Property(e => e.NetSalary)
                .HasPrecision(18, 2)
                .HasColumnName("net_salary");
            entity.Property(e => e.TotalShifts)
                .HasDefaultValue(0)
                .HasColumnName("total_shifts");
            entity.Property(e => e.Year).HasColumnName("year");

            entity.HasOne(d => d.Employee).WithMany(p => p.Salaries)
                .HasForeignKey(d => d.EmployeeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("salaries_employee_id_fkey");
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("shifts_pkey");

            entity.ToTable("shifts", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("tickets_pkey");

            entity.ToTable("tickets", "smart_pool");

            entity.HasIndex(e => e.QrCode, "tickets_qr_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.IssueDate)
                .HasDefaultValueSql("now()")
                .HasColumnName("issue_date");
            entity.Property(e => e.QrCode)
                .HasMaxLength(255)
                .HasColumnName("qr_code");
            entity.Property(e => e.RowVersion)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("row_version");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.TicketTypeId).HasColumnName("ticket_type_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.TicketType).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.TicketTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("tickets_ticket_type_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Tickets)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("tickets_user_id_fkey");
        });

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ticket_types_pkey");

            entity.ToTable("ticket_types", "smart_pool");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DurationDays).HasColumnName("duration_days");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .HasColumnName("name");
            entity.Property(e => e.Price)
                .HasPrecision(18, 2)
                .HasColumnName("price");
            entity.Property(e => e.TicketCategory)
                .HasMaxLength(50)
                .HasColumnName("ticket_category");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("users_pkey");

            entity.ToTable("users", "smart_pool");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.HasIndex(e => e.Phone, "users_phone_key").IsUnique();

            entity.HasIndex(e => e.Username, "users_username_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.Email)
                .HasMaxLength(150)
                .HasColumnName("email");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .HasColumnName("password_hash");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.RoleId).HasColumnName("role_id");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValueSql("'Active'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasColumnName("username");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("users_role_id_fkey");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("user_profiles_pkey");

            entity.ToTable("user_profiles", "smart_pool");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.Address).HasColumnName("address");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.DateOfBirth).HasColumnName("date_of_birth");
            entity.Property(e => e.FullName)
                .HasMaxLength(255)
                .HasColumnName("full_name");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("user_profiles_user_id_fkey");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("vouchers_pkey");

            entity.ToTable("vouchers", "smart_pool");

            entity.HasIndex(e => e.Code, "vouchers_code_key").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("uuid_generate_v4()")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("now()")
                .HasColumnName("created_at");
            entity.Property(e => e.DiscountType)
                .HasMaxLength(20)
                .HasColumnName("discount_type");
            entity.Property(e => e.DiscountValue)
                .HasPrecision(18, 2)
                .HasColumnName("discount_value");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDeleted)
                .HasDefaultValue(false)
                .HasColumnName("is_deleted");
            entity.Property(e => e.MinOrderValue)
                .HasPrecision(18, 2)
                .HasDefaultValueSql("0")
                .HasColumnName("min_order_value");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
