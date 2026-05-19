using SudanDialect.Api.Dtos;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Interfaces.Repositories;

public interface IWordRepository
{
    Task<Word?> GetActiveByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Word> Items, int TotalCount, int TotalPages, int BoundedPage)> GetActiveByFirstLetterPagedAsync(
        string rawLetter,
        string normalizedLetter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WordSearchCandidateDto>> SearchActiveByNormalizedQueryAsync(
        string normalizedQuery,
        int take,
        CancellationToken cancellationToken = default);

    Task<Feedback> AddFeedbackAsync(Feedback feedback, CancellationToken cancellationToken = default);

    Task<WordSuggestion> AddSuggestionAsync(WordSuggestion suggestion, CancellationToken cancellationToken = default);
}
