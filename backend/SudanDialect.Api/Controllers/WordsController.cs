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
}
