namespace SudanDialect.Api.Dtos;

public sealed class WordBrowsePageDto
{
    public IReadOnlyList<WordSearchResultDto> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
