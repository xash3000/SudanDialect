using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/word-suggestions")]
public sealed class AdminWordSuggestionsController : ControllerBase
{
    private readonly IAdminWordSuggestionService _adminWordSuggestionService;

    public AdminWordSuggestionsController(IAdminWordSuggestionService adminWordSuggestionService)
    {
        _adminWordSuggestionService = adminWordSuggestionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminWordSuggestionPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminWordSuggestionPageDto>> GetPage(
        [FromQuery] AdminWordSuggestionQueryDto request,
        CancellationToken cancellationToken)
    {
        var page = await _adminWordSuggestionService.GetPageAsync(request, cancellationToken);
        return Ok(page);
    }

    [HttpPatch("{id:int}/resolved")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> SetResolved(
        [FromRoute] int id,
        [FromBody] AdminSetWordSuggestionResolvedRequestDto request,
        CancellationToken cancellationToken)
    {
        var updated = await _adminWordSuggestionService.SetResolvedAsync(id, request.Resolved, cancellationToken);
        if (!updated)
        {
            return NotFound();
        }

        return Ok(new { id, resolved = request.Resolved });
    }
}
