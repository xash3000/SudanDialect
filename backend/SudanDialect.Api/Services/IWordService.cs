using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Services;

public interface IWordService
{
    Task<WordSearchResultDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default);
}
