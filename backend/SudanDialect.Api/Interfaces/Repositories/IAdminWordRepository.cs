using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Interfaces.Repositories;

public interface IAdminWordRepository
{
    Task<(IReadOnlyList<Word> Items, int TotalCount)> GetPagedAsync(
        string? rawFilter,
        string? normalizedFilter,
        int? wordIdFilter,
        bool useHeadwordSearch,
        bool? isActive,
        string sortBy,
        bool sortDescending,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<Word?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Word> AddAsync(Word word, CancellationToken cancellationToken = default);

    Task<Word?> UpdateAsync(
        int id,
        string headword,
        string normalizedHeadword,
        string definition,
        string normalizedDefinition,
        bool isActive,
        CancellationToken cancellationToken = default);

    Task<bool> SetInactiveAsync(int id, CancellationToken cancellationToken = default);

    Task<AdminDashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);
}
