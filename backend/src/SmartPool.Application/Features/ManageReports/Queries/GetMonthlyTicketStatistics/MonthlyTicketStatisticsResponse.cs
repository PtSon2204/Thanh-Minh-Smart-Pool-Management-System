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
