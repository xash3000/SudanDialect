using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Services;

public sealed class AdminWordVisitService : IAdminWordVisitService
{
    private readonly IAdminWordRepository _adminWordRepository;

    public AdminWordVisitService(IAdminWordRepository adminWordRepository)
    {
        _adminWordRepository = adminWordRepository;
    }

    public async Task<AdminWordVisitPageDto> GetWordVisitsAsync(AdminWordVisitQueryDto query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var sortBy = query.SortBy ?? "visitCount";
        var sortDescending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var (items, totalCount) = await _adminWordRepository.GetVisitsPagedAsync(
            sortBy,
            sortDescending,
            page,
            pageSize,
            ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new AdminWordVisitPageDto
        {
            Items = items.Select(word => new AdminWordVisitStatsDto
            {
                Id = word.Id,
                Headword = word.Headword,
                VisitCount = word.VisitCount,
                LastVisitedAt = word.LastVisitedAt
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }
}
