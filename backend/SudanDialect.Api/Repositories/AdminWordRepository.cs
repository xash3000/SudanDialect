using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Repositories;

public sealed class AdminWordRepository : IAdminWordRepository
{
    private readonly AppDbContext _dbContext;

    public AdminWordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Word> Items, int TotalCount)> GetPagedAsync(
        string? rawFilter,
        string? normalizedFilter,
        bool? isActive,
        string sortBy,
        bool sortDescending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Words.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(word => word.IsActive == isActive.Value);
        }

        var normalizedSearchTerm = !string.IsNullOrWhiteSpace(normalizedFilter)
            ? normalizedFilter.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSearchTerm) && !string.IsNullOrWhiteSpace(rawFilter))
        {
            normalizedSearchTerm = rawFilter.Trim();
        }

        if (!string.IsNullOrWhiteSpace(normalizedSearchTerm))
        {
            var rankedQuery = query
                .Select(word => new
                {
                    Word = word,
                    SimilarityScore = EF.Functions.TrigramsSimilarity(word.NormalizedHeadword, normalizedSearchTerm)
                })
                .Where(result => result.SimilarityScore > 0.0);

            var filteredTotalCount = await rankedQuery.CountAsync(cancellationToken);
            var filteredSkip = (page - 1) * pageSize;

            var filteredItems = await rankedQuery
                .OrderByDescending(result => result.SimilarityScore)
                .ThenBy(result => result.Word.Headword)
                .Skip(filteredSkip)
                .Take(pageSize)
                .Select(result => result.Word)
                .ToListAsync(cancellationToken);

            return (filteredItems, filteredTotalCount);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var sortedQuery = ApplySorting(query, sortBy, sortDescending);

        var skip = (page - 1) * pageSize;
        var items = await sortedQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<Word?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Words
            .AsNoTracking()
            .SingleOrDefaultAsync(word => word.Id == id, cancellationToken);
    }

    public async Task<Word> AddAsync(Word word, CancellationToken cancellationToken = default)
    {
        _dbContext.Words.Add(word);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return word;
    }

    public async Task<Word?> UpdateAsync(
        int id,
        string headword,
        string normalizedHeadword,
        string definition,
        string normalizedDefinition,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var word = await _dbContext.Words.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (word is null)
        {
            return null;
        }

        word.Headword = headword;
        word.NormalizedHeadword = normalizedHeadword;
        word.Definition = definition;
        word.NormalizedDefinition = normalizedDefinition;
        word.IsActive = isActive;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return word;
    }

    public async Task<bool> SetInactiveAsync(int id, CancellationToken cancellationToken = default)
    {
        var word = await _dbContext.Words.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (word is null)
        {
            return false;
        }

        if (!word.IsActive)
        {
            return true;
        }

        word.IsActive = false;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminDashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var totalWords = await _dbContext.Words.CountAsync(cancellationToken);
        var activeWords = await _dbContext.Words.CountAsync(word => word.IsActive, cancellationToken);

        return new AdminDashboardMetricsDto
        {
            TotalWords = totalWords,
            ActiveWords = activeWords,
            InactiveWords = totalWords - activeWords
        };
    }

    private static IQueryable<Word> ApplySorting(IQueryable<Word> query, string sortBy, bool sortDescending)
    {
        var normalizedSortBy = sortBy.Trim().ToLowerInvariant();

        return (normalizedSortBy, sortDescending) switch
        {
            ("id", true) => query.OrderByDescending(word => word.Id),
            ("id", false) => query.OrderBy(word => word.Id),
            ("headword", true) => query.OrderByDescending(word => word.Headword),
            ("headword", false) => query.OrderBy(word => word.Headword),
            ("createdat", true) => query.OrderByDescending(word => word.CreatedAt),
            ("createdat", false) => query.OrderBy(word => word.CreatedAt),
            ("updatedat", true) => query.OrderByDescending(word => word.UpdatedAt),
            ("updatedat", false) => query.OrderBy(word => word.UpdatedAt),
            ("isactive", true) => query.OrderByDescending(word => word.IsActive),
            ("isactive", false) => query.OrderBy(word => word.IsActive),
            (_, true) => query.OrderByDescending(word => word.UpdatedAt),
            (_, false) => query.OrderBy(word => word.UpdatedAt)
        };
    }
}
