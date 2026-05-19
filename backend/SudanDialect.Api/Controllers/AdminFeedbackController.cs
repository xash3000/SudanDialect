using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/feedback")]
public sealed class AdminFeedbackController : ControllerBase
{
    private readonly IAdminFeedbackService _adminFeedbackService;

    public AdminFeedbackController(IAdminFeedbackService adminFeedbackService)
    {
        _adminFeedbackService = adminFeedbackService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminFeedbackPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminFeedbackPageDto>> GetPage(
        [FromQuery] AdminFeedbackQueryDto request,
        CancellationToken cancellationToken)
    {
        var page = await _adminFeedbackService.GetPageAsync(request, cancellationToken);
        return Ok(page);
    }

    [HttpPatch("{id:int}/resolved")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> SetResolved(
        [FromRoute] int id,
        [FromBody] AdminSetFeedbackResolvedRequestDto request,
        CancellationToken cancellationToken)
    {
        var updated = await _adminFeedbackService.SetResolvedAsync(id, request.Resolved, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(new { id, resolved = request.Resolved });
    }
}
