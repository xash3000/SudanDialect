namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminDashboardMetricsDto
{
    public int TotalWords { get; init; }
    public int ActiveWords { get; init; }
    public int InactiveWords { get; init; }
}
