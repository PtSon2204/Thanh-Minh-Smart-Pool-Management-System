using System;
using System.Collections.Generic;

namespace SmartPool.Application.Features.ManageReports.Queries.GetMonthlyTicketStatistics;

public sealed class MonthlyTicketStatisticsResponse
{
    // === 1. KPI Cards ===
    public int TotalMembers { get; set; }
    public int NewMembersThisMonth { get; set; }
    public int ActiveTickets { get; set; }
    public double ActivePercentage { get; set; }
    public int ExpiringSoonTickets { get; set; }
    public int ExpiredTickets { get; set; }
    public double RenewalRate { get; set; }

    // === 2. Monthly Trend (Bar Chart: New vs Renewed per Month) ===
    public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();

    // === 3. Package Distribution (Donut Chart: Phân loại gói vé tháng) ===
    public List<PackageDistributionDto> PackageDistributions { get; set; } = new();

    // === 4. Member Warning List (Table: Danh sách thẻ/hội viên) ===
    public List<MemberTicketWarningDto> MemberWarnings { get; set; } = new();
}

public sealed class MonthlyTrendDto
{
    public string Month { get; set; } = string.Empty; // e.g. "T05", "T06"
    public int NewMembers { get; set; }
    public int RenewedMembers { get; set; }
}

public sealed class PackageDistributionDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = "#8b5cf6";
}

public sealed class MemberTicketWarningDto
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public int DaysRemaining { get; set; }
    public string Status { get; set; } = string.Empty; // "ACTIVE", "EXPIRING_SOON", "EXPIRED"
    public int TotalEntriesUsed { get; set; }
}
