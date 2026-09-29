using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class Order
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? EmployeeId { get; set; }

    public Guid? VoucherId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal? DiscountAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string? Status { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Employee? Employee { get; set; }

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Rental> Rentals { get; set; } = new List<Rental>();

    public virtual User? User { get; set; }

    public virtual Voucher? Voucher { get; set; }
}

