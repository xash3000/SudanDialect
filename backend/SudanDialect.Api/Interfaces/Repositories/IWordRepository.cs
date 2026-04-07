using SudanDialect.Api.Dtos;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Interfaces.Repositories;

public interface IWordRepository
{
    Task<WordSearchResultDto?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Word>> GetActiveByFirstLetterAsync(
        string rawLetter,
        string normalizedLetter,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default);
}
