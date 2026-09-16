using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Utilities;
using System.Text.RegularExpressions;

namespace SudanDialect.Api.Services;

public sealed class AdminWordService : IAdminWordService
{
    private const int MaxPageSize = 200;
    private const int MaxAuditPageSize = 200;
    private const int MaxFilterLength = 200;

    private static readonly HashSet<string> AllowedSortFields = ["id", "headword", "createdat", "updatedat", "isactive"];
    private static readonly HashSet<string> AllowedSearchFields = ["headword", "id"];
    private static readonly Regex ArabicTextRegex = new("[\\u0600-\\u06FF]", RegexOptions.Compiled);

    private readonly IAdminWordRepository _adminWordRepository;
    private readonly ITextEmbeddingService? _textEmbeddingService;

    public AdminWordService(
        IAdminWordRepository adminWordRepository,
        ITextEmbeddingService? textEmbeddingService = null)
    {
        _adminWordRepository = adminWordRepository;
        _textEmbeddingService = textEmbeddingService;
    }

    public Task<AdminDashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        return _adminWordRepository.GetMetricsAsync(cancellationToken);
    }

    public async Task<WordPageDto> GetPageAsync(AdminWordTableQueryDto query, CancellationToken cancellationToken = default)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, MaxPageSize);
        var rawFilter = query.Query?.Trim();
        var normalizedSearchBy = NormalizeSearchBy(query.SearchBy);
        var useHeadwordSearch = normalizedSearchBy == "headword";
        var wordIdFilter = default(int?);

        if (!string.IsNullOrWhiteSpace(rawFilter) && rawFilter.Length > MaxFilterLength)
        {
            throw new ArgumentException($"Filter length cannot exceed {MaxFilterLength} characters.", nameof(query.Query));
        }

        if (!string.IsNullOrWhiteSpace(rawFilter) && !useHeadwordSearch)
        {
            if (!int.TryParse(rawFilter, out var parsedWordId) || parsedWordId <= 0)
            {
                throw new ArgumentException("Word id filter must be a positive integer.", nameof(query.Query));
            }

            wordIdFilter = parsedWordId;
        }

        var normalizedFilter = string.IsNullOrWhiteSpace(rawFilter) || !useHeadwordSearch
            ? string.Empty
            : ArabicTextNormalizer.Normalize(rawFilter);

        var normalizedSortBy = NormalizeSortBy(query.SortBy);
        var sortDescending = NormalizeSortDirection(query.SortDirection) == "desc";

        var (items, totalCount) = await _adminWordRepository.GetPagedAsync(
            rawFilter,
            normalizedFilter,
            wordIdFilter,
            useHeadwordSearch,
            query.IsActive,
            normalizedSortBy,
            sortDescending,
            page,
            pageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var boundedPage = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        if (totalPages > 0 && page > totalPages)
        {
            (items, _) = await _adminWordRepository.GetPagedAsync(
                rawFilter,
                normalizedFilter,
                wordIdFilter,
                useHeadwordSearch,
                query.IsActive,
                normalizedSortBy,
                sortDescending,
                boundedPage,
                pageSize,
                cancellationToken);
        }

        return new WordPageDto
        {
            Items = items,
            Page = boundedPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<Word?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Word id must be a positive integer.");
        }

        return await _adminWordRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<Word> CreateAsync(
        AdminCreateWordRequestDto request,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new ArgumentException("Admin user id is required.", nameof(adminUserId));
        }

        var headword = ValidateArabicText(request.Headword, nameof(request.Headword), 200);
        var definition = ValidateArabicText(request.Definition, nameof(request.Definition), 4000);

        var normalizedHeadword = ArabicTextNormalizer.Normalize(headword);
        var normalizedDefinition = ArabicTextNormalizer.Normalize(definition);

        Pgvector.Vector? embedding = null;
        if (_textEmbeddingService != null && _textEmbeddingService.IsAvailable)
        {
            var textToEmbed = $"{normalizedHeadword} {normalizedDefinition}";
            cancellationToken.ThrowIfCancellationRequested();
            embedding = _textEmbeddingService.GenerateEmbedding(textToEmbed);
        }

        var word = new Word
        {
            Headword = headword,
            Definition = definition,
            NormalizedHeadword = normalizedHeadword,
            NormalizedDefinition = normalizedDefinition,
            IsActive = request.IsActive,
            Embedding = embedding
        };

        return await _adminWordRepository.AddAsync(word, adminUserId, clientIp, userAgent, cancellationToken);
    }

    public async Task<Word?> UpdateAsync(
        int id,
        AdminUpdateWordRequestDto request,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Word id must be a positive integer.");
        }

        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new ArgumentException("Admin user id is required.", nameof(adminUserId));
        }

        var headword = ValidateArabicText(request.Headword, nameof(request.Headword), 200);
        var definition = ValidateArabicText(request.Definition, nameof(request.Definition), 4000);

        var normalizedHeadword = ArabicTextNormalizer.Normalize(headword);
        var normalizedDefinition = ArabicTextNormalizer.Normalize(definition);

        Pgvector.Vector? embedding = null;
        if (_textEmbeddingService != null && _textEmbeddingService.IsAvailable)
        {
            var textToEmbed = $"{normalizedHeadword} {normalizedDefinition}";
            cancellationToken.ThrowIfCancellationRequested();
            embedding = _textEmbeddingService.GenerateEmbedding(textToEmbed);
        }

        return await _adminWordRepository.UpdateAsync(
            id,
            headword,
            normalizedHeadword,
            definition,
            normalizedDefinition,
            request.IsActive,
            adminUserId,
            clientIp,
            userAgent,
            embedding,
            cancellationToken);
    }

    public async Task<bool> DeactivateAsync(
        int id,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Word id must be a positive integer.");
        }

        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            throw new ArgumentException("Admin user id is required.", nameof(adminUserId));
        }

        return await _adminWordRepository.SetInactiveAsync(id, adminUserId, clientIp, userAgent, cancellationToken);
    }

    public Task<AdminWordEditAuditPageDto> GetAuditPageAsync(
        AdminWordEditAuditQueryDto query,
        CancellationToken cancellationToken = default)
    {
        return GetAuditPageInternalAsync(null, query, cancellationToken);
    }

    public Task<AdminWordEditAuditPageDto> GetAuditPageByWordIdAsync(
        int wordId,
        AdminWordEditAuditQueryDto query,
        CancellationToken cancellationToken = default)
    {
        if (wordId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(wordId), "Word id must be a positive integer.");
        }

        return GetAuditPageInternalAsync(wordId, query, cancellationToken);
    }

    private async Task<AdminWordEditAuditPageDto> GetAuditPageInternalAsync(
        int? wordId,
        AdminWordEditAuditQueryDto query,
        CancellationToken cancellationToken)
    {
        var page = query.Page <= 0 ? 1 : query.Page;
        var pageSize = query.PageSize <= 0 ? 20 : Math.Min(query.PageSize, MaxAuditPageSize);
        var actionType = NormalizeAuditActionType(query.ActionType);
        var sortDescending = NormalizeSortDirection(query.SortDirection) == "desc";

        var (items, totalCount) = await _adminWordRepository.GetAuditPagedAsync(
            wordId,
            actionType,
            sortDescending,
            page,
            pageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var boundedPage = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        if (totalPages > 0 && page > totalPages)
        {
            (items, _) = await _adminWordRepository.GetAuditPagedAsync(
                wordId,
                actionType,
                sortDescending,
                boundedPage,
                pageSize,
                cancellationToken);
        }

        return new AdminWordEditAuditPageDto
        {
            Items = items,
            Page = boundedPage,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    private static string ValidateArabicText(string? input, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Field is required.", parameterName);
        }

        var trimmed = input.Trim();

        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Field length cannot exceed {maxLength} characters.", parameterName);
        }

        if (!ArabicTextRegex.IsMatch(trimmed))
        {
            throw new ArgumentException("Field must contain Arabic characters.", parameterName);
        }

        return trimmed;
    }

    private static string NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "updatedat";
        }

        var normalized = sortBy.Trim().ToLowerInvariant();
        return AllowedSortFields.Contains(normalized) ? normalized : "updatedat";
    }

    private static string NormalizeSortDirection(string? sortDirection)
    {
        if (string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase))
        {
            return "asc";
        }

        return "desc";
    }

    private static string NormalizeSearchBy(string? searchBy)
    {
        if (string.IsNullOrWhiteSpace(searchBy))
        {
            return "headword";
        }

        var normalized = searchBy.Trim().ToLowerInvariant();
        return AllowedSearchFields.Contains(normalized) ? normalized : "headword";
    }

    private static string? NormalizeAuditActionType(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return null;
        }

        var normalized = actionType.Trim().ToLowerInvariant();
        if (!AdminWordEditActionTypes.Allowed.Contains(normalized))
        {
            throw new ArgumentException("Unsupported actionType value.", nameof(actionType));
        }

        return normalized;
    }
}
