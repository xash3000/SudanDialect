using SudanDialect.Api.Dtos;

namespace SudanDialect.Api.Interfaces.Services;

public interface IWordService
{
    Task<WordDetailsDto?> GetByPublicIdAsync(string publicId, CancellationToken cancellationToken = default);

    Task<WordBrowsePageDto> BrowseByLetterAsync(
        string? rawLetter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchResultDto>> SemanticSearchAsync(string? rawQuery, CancellationToken cancellationToken = default);

    Task<bool> SubmitFeedbackAsync(
        string publicWordId,
        string? feedbackText,
        string? captchaToken,
        string? remoteIp,
        CancellationToken cancellationToken = default);

    Task<bool> SubmitSuggestionAsync(
        string? headword,
        string? definition,
        string? email,
        string? captchaToken,
        string? remoteIp,
        CancellationToken cancellationToken = default);
}
