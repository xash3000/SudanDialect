namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminWordVisitQueryDto
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; } // "visitCount", "headword", "lastVisitedAt"
    public string? SortDirection { get; init; } // "asc", "desc"
}
