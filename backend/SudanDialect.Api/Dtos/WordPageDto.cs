using SudanDialect.Api.Models;

namespace SudanDialect.Api.Dtos;

public sealed class WordPageDto
{
    public IReadOnlyList<Word> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}
