using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    [ProducesResponseType(typeof(WordPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WordPageDto>> GetPage(
        [FromQuery] AdminWordTableQueryDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var page = await _adminWordService.GetPageAsync(request, cancellationToken);
            return Ok(page);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Word), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Word>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var word = await _adminWordService.GetByIdAsync(id, cancellationToken);
            if (word is null)
            {
                return NotFound();
            }

            return Ok(word);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(Word), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Word>> Create(
        [FromBody] AdminCreateWordRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var createdWord = await _adminWordService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = createdWord.Id }, createdWord);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
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
        try
        {
            var updatedWord = await _adminWordService.UpdateAsync(id, request, cancellationToken);
            if (updatedWord is null)
            {
                return NotFound();
            }

            return Ok(updatedWord);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> Deactivate([FromRoute] int id, CancellationToken cancellationToken)
    {
        try
        {
            var deactivated = await _adminWordService.DeactivateAsync(id, cancellationToken);
            if (!deactivated)
            {
                return NotFound();
            }

            return Ok(new { id, deactivated = true });
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
