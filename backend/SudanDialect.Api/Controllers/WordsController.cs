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

    [HttpGet("{id}")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsGetByIdPerIp)]
    [ProducesResponseType(typeof(WordDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WordDetailsDto>> GetById(
        [FromRoute] string id,
        CancellationToken cancellationToken)
    {
        var word = await _wordService.GetByPublicIdAsync(id, cancellationToken);
        if (word is null)
        {
            return NotFound();
        }

        return Ok(word);
    }

    [HttpGet("search")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsSearchPerIp)]
    [ProducesResponseType(typeof(IReadOnlyList<WordSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WordSearchResultDto>>> Search(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
    {
        var results = await _wordService.SearchAsync(query, cancellationToken);
        return Ok(results);
    }

    [HttpGet("browse")]
    [EnableRateLimiting(RateLimitPolicyNames.WordsBrowsePerIp)]
    [ProducesResponseType(typeof(WordBrowsePageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WordBrowsePageDto>> BrowseByLetter(
        [FromQuery] string? letter,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 40)
    {
        var results = await _wordService.BrowseByLetterAsync(letter, page, pageSize, cancellationToken);
        return Ok(results);
    }

    [HttpPost("{id}/feedback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> SubmitFeedback(
        [FromRoute] string id,
        [FromBody] SubmitWordFeedbackRequestDto request,
        CancellationToken cancellationToken)
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

    [HttpPost("suggestions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> SubmitSuggestion(
        [FromBody] SubmitWordSuggestionRequestDto request,
        CancellationToken cancellationToken)
    {
        var submitted = await _wordService.SubmitSuggestionAsync(
            request.Headword,
            request.Definition,
            request.Email,
            request.CaptchaToken,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return Ok(new { submitted });
    }
}
