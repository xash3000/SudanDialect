using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Interfaces.Services;

public interface IAdminWordService
{
    Task<AdminDashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);

    Task<WordPageDto> GetPageAsync(AdminWordTableQueryDto query, CancellationToken cancellationToken = default);

    Task<Word?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Word> CreateAsync(AdminCreateWordRequestDto request, CancellationToken cancellationToken = default);

    Task<Word?> UpdateAsync(int id, AdminUpdateWordRequestDto request, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, CancellationToken cancellationToken = default);
}
