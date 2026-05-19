using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/words")]
public sealed class AdminWordsController : ControllerBase
{
    private readonly IAdminWordService _adminWordService;

    public AdminWordsController(IAdminWordService adminWordService)
    {
        _adminWordService = adminWordService;
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(AdminDashboardMetricsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminDashboardMetricsDto>> GetMetrics(CancellationToken cancellationToken)
    {
        var metrics = await _adminWordService.GetMetricsAsync(cancellationToken);
        return Ok(metrics);
    }

    [HttpGet("audit")]
    [ProducesResponseType(typeof(AdminWordEditAuditPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminWordEditAuditPageDto>> GetAuditPage(
        [FromQuery] AdminWordEditAuditQueryDto request,
        CancellationToken cancellationToken)
    {
        var page = await _adminWordService.GetAuditPageAsync(request, cancellationToken);
        return Ok(page);
    }

    [HttpGet]
    [ProducesResponseType(typeof(WordPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WordPageDto>> GetPage(
        [FromQuery] AdminWordTableQueryDto request,
        CancellationToken cancellationToken)
    {
        var page = await _adminWordService.GetPageAsync(request, cancellationToken);
        return Ok(page);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Word), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Word>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var word = await _adminWordService.GetByIdAsync(id, cancellationToken);
        if (word is null)
        {
            return NotFound();
        }

        return Ok(word);
    }

    [HttpGet("{id:int}/audit")]
    [ProducesResponseType(typeof(AdminWordEditAuditPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminWordEditAuditPageDto>> GetAuditPageByWordId(
        [FromRoute] int id,
        [FromQuery] AdminWordEditAuditQueryDto request,
        CancellationToken cancellationToken)
    {
        var page = await _adminWordService.GetAuditPageByWordIdAsync(id, request, cancellationToken);
        return Ok(page);
    }

    [HttpPost]
    [ProducesResponseType(typeof(Word), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Word>> Create(
        [FromBody] AdminCreateWordRequestDto request,
        CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized(new { error = "Authenticated user id is missing." });
        }

        var createdWord = await _adminWordService.CreateAsync(
            request,
            adminUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = createdWord.Id }, createdWord);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(Word), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Word>> Update(
        [FromRoute] int id,
        [FromBody] AdminUpdateWordRequestDto request,
        CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized(new { error = "Authenticated user id is missing." });
        }

        var updatedWord = await _adminWordService.UpdateAsync(
            id,
            request,
            adminUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (updatedWord is null)
        {
            return NotFound();
        }

        return Ok(updatedWord);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> Deactivate([FromRoute] int id, CancellationToken cancellationToken)
    {
        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(adminUserId))
        {
            return Unauthorized(new { error = "Authenticated user id is missing." });
        }

        var deactivated = await _adminWordService.DeactivateAsync(
            id,
            adminUserId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            cancellationToken);

        if (!deactivated)
        {
            return NotFound();
        }

        return Ok(new { id, deactivated = true });
    }
}
