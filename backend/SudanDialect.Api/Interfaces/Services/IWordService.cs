using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Interfaces.Services;

public interface IWordService
{
    Task<WordSearchResultDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<WordPageDto> BrowseByLetterAsync(
        string? rawLetter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default);
}
