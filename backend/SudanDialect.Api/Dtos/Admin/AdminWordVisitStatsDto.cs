namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminWordVisitStatsDto
{
    public int Id { get; init; }
    public string Headword { get; init; } = string.Empty;
    public int VisitCount { get; init; }
    public DateTime? LastVisitedAt { get; init; }
}
