using SudanDialect.Api.Dtos;
using SudanDialect.Api.Repositories;
using SudanDialect.Api.Utilities;

namespace SudanDialect.Api.Services;

public sealed class WordService : IWordService
{
    private const int MaxQueryLength = 200;
    private const int MaxResults = 10;

    private readonly IWordRepository _wordRepository;

    public WordService(IWordRepository wordRepository)
    {
        _wordRepository = wordRepository;
    }

    public async Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        if (rawQuery.Length > MaxQueryLength)
        {
            throw new ArgumentException($"Query length cannot exceed {MaxQueryLength} characters.", nameof(rawQuery));
        }

        var normalizedQuery = ArabicTextNormalizer.Normalize(rawQuery);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        var searchResults = await _wordRepository.SearchActiveByNormalizedQueryAsync(
            normalizedQuery,
            MaxResults,
            cancellationToken);

        return searchResults;
    }
}
