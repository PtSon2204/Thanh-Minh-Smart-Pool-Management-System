using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class Salary
{
    public Guid Id { get; set; }

    public Guid EmployeeId { get; set; }

    public int Month { get; set; }

    public int Year { get; set; }

    public int? TotalShifts { get; set; }

    public decimal? Bonus { get; set; }

    public decimal? Deduction { get; set; }

    public decimal NetSalary { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Employee Employee { get; set; } = null!;
}
