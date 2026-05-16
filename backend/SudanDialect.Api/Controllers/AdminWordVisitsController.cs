using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/words/visits")]
public sealed class AdminWordVisitsController : ControllerBase
{
    private readonly IAdminWordVisitService _adminWordVisitService;

    public AdminWordVisitsController(IAdminWordVisitService adminWordVisitService)
    {
        _adminWordVisitService = adminWordVisitService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminWordVisitPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminWordVisitPageDto>> GetVisits(
        [FromQuery] AdminWordVisitQueryDto query,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _adminWordVisitService.GetWordVisitsAsync(query, cancellationToken);
            return Ok(page);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
