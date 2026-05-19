using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Repositories;

public sealed class WordRepository : IWordRepository
{
    // private const double HeadwordWeight = 10.0;

    private readonly AppDbContext _dbContext;

    public WordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Word?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var word = await _dbContext.Words
            .Where(word => word.IsActive && word.Id == id)
            .SingleOrDefaultAsync(cancellationToken);

        if (word is not null)
        {
            word.VisitCount++;
            word.LastVisitedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return word;
    }

    public async Task<(IReadOnlyList<Word> Items, int TotalCount, int TotalPages, int BoundedPage)> GetActiveByFirstLetterPagedAsync(
        string rawLetter,
        string normalizedLetter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawLetter) || string.IsNullOrWhiteSpace(normalizedLetter))
        {
            return (Array.Empty<Word>(), 0, 0, 1);
        }

        var query = _dbContext.Words
            .AsNoTracking()
            .Where(word => word.IsActive)
            .Where(word =>
                word.Headword.StartsWith(rawLetter)
                || word.NormalizedHeadword.StartsWith(normalizedLetter));

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        var boundedPage = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        var items = await query
            .OrderBy(word => word.Headword)
            .Skip((boundedPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount, totalPages, boundedPage);
    }

    public async Task<IReadOnlyList<WordSearchCandidateDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<WordSearchCandidateDto>();
        }

        return await _dbContext.Words
            .AsNoTracking()
            .Where(word => word.IsActive)
            .Select(word => new WordSearchCandidateDto
            {
                Id = word.Id,
                Headword = word.Headword,
                SimilarityScore =
                    EF.Functions.TrigramsSimilarity(word.NormalizedHeadword, normalizedQuery)
                // EF.Functions.TrigramsSimilarity(word.NormalizedDefinition, normalizedQuery)
            })
            .Where(result => result.SimilarityScore > 0.0)
            .OrderByDescending(result => result.SimilarityScore)
            .ThenBy(result => result.Headword)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Feedback> AddFeedbackAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        _dbContext.Feedback.Add(feedback);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return feedback;
    }

    public async Task<WordSuggestion> AddSuggestionAsync(WordSuggestion suggestion, CancellationToken cancellationToken = default)
    {
        _dbContext.WordSuggestions.Add(suggestion);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return suggestion;
    }
}
