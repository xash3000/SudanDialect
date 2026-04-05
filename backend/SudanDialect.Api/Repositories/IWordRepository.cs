using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Repositories;

public interface IWordRepository
{
    Task<WordSearchResultDto?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> GetActiveByFirstLetterAsync(
        string rawLetter,
        string normalizedLetter,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default);
}
