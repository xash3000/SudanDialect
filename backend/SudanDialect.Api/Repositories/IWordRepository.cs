using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Repositories;

public interface IWordRepository
{
    Task<IReadOnlyList<WordSearchResultDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default);
}
