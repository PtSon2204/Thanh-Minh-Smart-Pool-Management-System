using System;
using System.Collections.Generic;

namespace SmartPool.Domain.Entities;

public partial class UserProfile
{
    public Guid UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? AvatarUrl { get; set; }

    public string? Address { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    // Optional demographic value: "MALE" or "FEMALE"; legacy rows stay null.
    public string? Gender { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}

