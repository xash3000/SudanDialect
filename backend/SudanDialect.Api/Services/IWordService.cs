using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Services;

public interface IWordService
{
    Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default);
}
