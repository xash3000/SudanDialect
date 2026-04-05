using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Services;

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
    [ProducesResponseType(typeof(IReadOnlyList<WordSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WordSearchResultDto>>> BrowseByLetter(
        [FromQuery] string? letter,
        CancellationToken cancellationToken)
    {
        try
        {
            var results = await _wordService.BrowseByLetterAsync(letter, cancellationToken);
            return Ok(results);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
