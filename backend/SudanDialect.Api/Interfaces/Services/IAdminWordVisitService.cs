using SudanDialect.Api.Dtos.Admin;

namespace SudanDialect.Api.Interfaces.Services;

public interface IAdminWordVisitService
{
    Task<AdminWordVisitPageDto> GetWordVisitsAsync(AdminWordVisitQueryDto query, CancellationToken ct = default);
}
