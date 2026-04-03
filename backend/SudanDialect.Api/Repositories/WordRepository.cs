using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Repositories;

public sealed class WordRepository : IWordRepository
{
    // private const double HeadwordWeight = 10.0;

    private readonly AppDbContext _dbContext;

    public WordRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<WordSearchResultDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        return await _dbContext.Words
            .AsNoTracking()
            .Where(word => word.IsActive)
            .Select(word => new WordSearchResultDto
            {
                Id = word.Id,
                Headword = word.Headword,
                Definition = word.Definition,
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
}
