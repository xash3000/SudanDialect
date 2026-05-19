using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Models;
using SudanDialect.Api.Utilities;

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
        int? wordIdFilter,
        bool useHeadwordSearch,
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

        if (wordIdFilter.HasValue)
        {
            query = query.Where(word => word.Id == wordIdFilter.Value);
        }

        var normalizedSearchTerm = !string.IsNullOrWhiteSpace(normalizedFilter)
            ? normalizedFilter.Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedSearchTerm) && !string.IsNullOrWhiteSpace(rawFilter))
        {
            normalizedSearchTerm = rawFilter.Trim();
        }

        if (useHeadwordSearch && !string.IsNullOrWhiteSpace(normalizedSearchTerm))
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

    public async Task<Word> AddAsync(
        Word word,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Words.Add(word);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _dbContext.Audits.Add(CreateAuditEntry(
            word,
            adminUserId,
            string.Empty,
            word.Headword,
            string.Empty,
            word.Definition,
            false,
            word.IsActive,
            string.Empty,
            word.NormalizedHeadword,
            string.Empty,
            word.NormalizedDefinition,
            clientIp,
            userAgent,
            AdminWordEditActionTypes.Create));

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return word;
    }

    public async Task<Word?> UpdateAsync(
        int id,
        string headword,
        string normalizedHeadword,
        string definition,
        string normalizedDefinition,
        bool isActive,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var word = await _dbContext.Words.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (word is null)
        {
            return null;
        }

        var oldHeadword = word.Headword;
        var oldNormalizedHeadword = word.NormalizedHeadword;
        var oldDefinition = word.Definition;
        var oldNormalizedDefinition = word.NormalizedDefinition;
        var oldIsActive = word.IsActive;

        word.Headword = headword;
        word.NormalizedHeadword = normalizedHeadword;
        word.Definition = definition;
        word.NormalizedDefinition = normalizedDefinition;
        word.IsActive = isActive;

        _dbContext.Audits.Add(CreateAuditEntry(
            word,
            adminUserId,
            oldHeadword,
            headword,
            oldDefinition,
            definition,
            oldIsActive,
            isActive,
            oldNormalizedHeadword,
            normalizedHeadword,
            oldNormalizedDefinition,
            normalizedDefinition,
            clientIp,
            userAgent));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return word;
    }

    public async Task<bool> SetInactiveAsync(
        int id,
        string adminUserId,
        string? clientIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
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

        var oldHeadword = word.Headword;
        var oldNormalizedHeadword = word.NormalizedHeadword;
        var oldDefinition = word.Definition;
        var oldNormalizedDefinition = word.NormalizedDefinition;
        var oldIsActive = word.IsActive;

        word.IsActive = false;

        _dbContext.Audits.Add(CreateAuditEntry(
            word,
            adminUserId,
            oldHeadword,
            oldHeadword,
            oldDefinition,
            oldDefinition,
            oldIsActive,
            false,
            oldNormalizedHeadword,
            oldNormalizedHeadword,
            oldNormalizedDefinition,
            oldNormalizedDefinition,
            clientIp,
            userAgent));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<(IReadOnlyList<AdminWordEditAuditEntryDto> Items, int TotalCount)> GetAuditPagedAsync(
        int? wordId,
        string? actionType,
        bool sortDescending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Audits
            .AsNoTracking();

        if (wordId.HasValue)
        {
            query = query.Where(audit => audit.WordId == wordId.Value);
        }

        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(audit => audit.ActionType == actionType);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var sortedQuery = sortDescending
            ? query.OrderByDescending(audit => audit.EditedAt).ThenByDescending(audit => audit.Id)
            : query.OrderBy(audit => audit.EditedAt).ThenBy(audit => audit.Id);

        var skip = (page - 1) * pageSize;

        var items = await (
            from audit in sortedQuery.Skip(skip).Take(pageSize)
            join user in _dbContext.Users.AsNoTracking() on audit.AdminUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            select new AdminWordEditAuditEntryDto
            {
                Id = audit.Id,
                WordId = audit.WordId,
                WordHeadword = audit.Word.Headword,
                AdminUserId = audit.AdminUserId,
                AdminDisplayName = user != null && !string.IsNullOrEmpty(user.UserName)
                    ? user.UserName!
                    : user != null && !string.IsNullOrEmpty(user.Email)
                        ? user.Email!
                        : audit.AdminUserId,
                EditedAt = audit.EditedAt,
                ActionType = audit.ActionType,
                OldHeadword = audit.OldHeadword,
                NewHeadword = audit.NewHeadword,
                OldDefinition = audit.OldDefinition,
                NewDefinition = audit.NewDefinition,
                OldIsActive = audit.OldIsActive,
                NewIsActive = audit.NewIsActive,
                OldNormalizedHeadword = audit.OldNormalizedHeadword,
                NewNormalizedHeadword = audit.NewNormalizedHeadword,
                OldNormalizedDefinition = audit.OldNormalizedDefinition,
                NewNormalizedDefinition = audit.NewNormalizedDefinition,
                ClientIp = audit.ClientIp,
                UserAgent = audit.UserAgent
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount);
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

    private static Audit CreateAuditEntry(
        Word word,
        string adminUserId,
        string oldHeadword,
        string newHeadword,
        string oldDefinition,
        string newDefinition,
        bool oldIsActive,
        bool newIsActive,
        string oldNormalizedHeadword,
        string newNormalizedHeadword,
        string oldNormalizedDefinition,
        string newNormalizedDefinition,
        string? clientIp,
        string? userAgent,
        string? actionTypeOverride = null)
    {
        return new Audit
        {
            WordId = word.Id,
            AdminUserId = adminUserId,
            EditedAt = DateTime.UtcNow,
            ActionType = actionTypeOverride ?? AdminWordEditActionTypes.Resolve(oldIsActive, newIsActive),
            OldHeadword = oldHeadword,
            NewHeadword = newHeadword,
            OldDefinition = oldDefinition,
            NewDefinition = newDefinition,
            OldIsActive = oldIsActive,
            NewIsActive = newIsActive,
            OldNormalizedHeadword = oldNormalizedHeadword,
            NewNormalizedHeadword = newNormalizedHeadword,
            OldNormalizedDefinition = oldNormalizedDefinition,
            NewNormalizedDefinition = newNormalizedDefinition,
            ClientIp = SanitizeOptionalValue(clientIp, 100),
            UserAgent = SanitizeOptionalValue(userAgent, 512)
        };
    }

    private static string? SanitizeOptionalValue(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength];
    }
}
