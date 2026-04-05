using SudanDialect.Api.Dtos;
using SudanDialect.Api.Repositories;
using SudanDialect.Api.Utilities;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SudanDialect.Api.Services;

public sealed class WordService : IWordService
{
    private const int MaxQueryLength = 200;
    private const int MaxResults = 10;
    private const int MaxBrowseResults = 10000;

    private static readonly Regex ArabicLetterRegex = new("^[\\u0621-\\u064A]$", RegexOptions.Compiled);
    private static readonly CompareInfo ArabicCompareInfo = CultureInfo.GetCultureInfo("ar").CompareInfo;

    private readonly IWordRepository _wordRepository;

    public WordService(IWordRepository wordRepository)
    {
        _wordRepository = wordRepository;
    }

    public async Task<WordSearchResultDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Word id must be a positive integer.");
        }

        return await _wordRepository.GetActiveByIdAsync(id, cancellationToken);
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

    public async Task<IReadOnlyList<WordSearchResultDto>> BrowseByLetterAsync(string? rawLetter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawLetter))
        {
            throw new ArgumentException("Letter is required.", nameof(rawLetter));
        }

        var trimmedLetter = rawLetter.Trim();
        if (trimmedLetter.Length != 1 || !ArabicLetterRegex.IsMatch(trimmedLetter))
        {
            throw new ArgumentException("Letter must be a single Arabic character.", nameof(rawLetter));
        }

        var normalizedLetter = ArabicTextNormalizer.Normalize(trimmedLetter);
        if (string.IsNullOrWhiteSpace(normalizedLetter))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        var words = await _wordRepository.GetActiveByFirstLetterAsync(
            trimmedLetter,
            normalizedLetter,
            MaxBrowseResults,
            cancellationToken);

        return words
            .OrderBy(word => word.Headword, Comparer<string>.Create((first, second) =>
                ArabicCompareInfo.Compare(first, second, CompareOptions.None)))
            .ToList();
    }
}
