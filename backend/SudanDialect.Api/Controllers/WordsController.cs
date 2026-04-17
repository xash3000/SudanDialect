using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Utilities;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class WordsController : ControllerBase
{
    private readonly IWordService _wordService;

    public WordsController(IWordService wordService)
    {
        _wordService = wordService;
    }

    [HttpGet("{id:int}")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsGetByIdPerIp)]
    [ProducesResponseType(typeof(WordSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WordSearchResultDto>> GetById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var word = await _wordService.GetByIdAsync(id, cancellationToken);
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

    [HttpGet("search")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsSearchPerIp)]
    [ProducesResponseType(typeof(IReadOnlyList<WordSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WordSearchResultDto>>> Search(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _wordService.SearchAsync(query, cancellationToken);
            return Ok(results);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("browse")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsBrowsePerIp)]
    [ProducesResponseType(typeof(WordPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WordPageDto>> BrowseByLetter(
        [FromQuery] string? letter,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 80)
    {
        try
        {
            var results = await _wordService.BrowseByLetterAsync(letter, page, pageSize, cancellationToken);
            return Ok(results);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:int}/feedback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> SubmitFeedback(
        [FromRoute] int id,
        [FromBody] SubmitWordFeedbackRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var submitted = await _wordService.SubmitFeedbackAsync(
                id,
                request.FeedbackText,
                request.CaptchaToken,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            if (!submitted)
            {
                return NotFound();
            }

            return Ok(new { submitted = true });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
