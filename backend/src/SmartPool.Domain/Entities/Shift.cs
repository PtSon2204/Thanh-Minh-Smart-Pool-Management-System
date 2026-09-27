using System;
using System.Collections.Generic;

namespace SmartPool.Infrastructure.Persistence.TempModels;

public partial class Shift
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool? IsActive { get; set; }

    public virtual ICollection<EmployeeShift> EmployeeShifts { get; set; } = new List<EmployeeShift>();
}
