using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Services;
using System.Net;

namespace SudanDialect.Tests.Unit.Controllers;

public class WordsControllerTests
{
    private readonly Mock<IWordService> _wordServiceMock;
    private readonly WordsController _sut;

    public WordsControllerTests()
    {
        _wordServiceMock = new Mock<IWordService>();
        _sut = new WordsController(_wordServiceMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenRequestIsValidAndWordExists()
    {
        // arrange
        var publicId = "abcd1234";
        var expectedWord = new WordDetailsDto
        {
            Id = publicId,
            Headword = "تجربة",
            Definition = "معنى تجربة"
        };
        _wordServiceMock
            .Setup(wordService => wordService.GetByPublicIdAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWord);

        // act
        var actionResult = await _sut.GetById(publicId, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedWord);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenWordDoesNotExist()
    {
        // arrange
        var publicId = "abcd1234";
        _wordServiceMock
            .Setup(wordService => wordService.GetByPublicIdAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WordDetailsDto?)null);

        // act
        var actionResult = await _sut.GetById(publicId, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ShouldReturnBadRequest_WhenArgumentOutOfRangeExceptionIsThrown()
    {
        // arrange
        var publicId = "abcd1234";
        _wordServiceMock
            .Setup(wordService => wordService.GetByPublicIdAsync(publicId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("id", "Invalid ID"));

        // act
        var actionResult = await _sut.GetById(publicId, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Search_ShouldReturnOk_WithResults()
    {
        // arrange
        var query = "test";
        var expectedResults = new List<WordSearchResultDto>
        {
            new() { Id = "1", Headword = "تجربة", SimilarityScore = 1f }
        };
        _wordServiceMock
            .Setup(ws => ws.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // act
        var actionResult = await _sut.Search(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedResults);
    }

    [Fact]
    public async Task Search_ShouldReturnBadRequest_WhenArgumentExceptionIsThrown()
    {
        // arrange
        var query = new string('a', 300);
        _wordServiceMock
            .Setup(ws => ws.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Too long"));

        // act
        var actionResult = await _sut.Search(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BrowseByLetter_ShouldReturnOk_WithResults()
    {
        // arrange
        var letter = "ب";
        var expectedPage = new WordBrowsePageDto
        {
            Items = new List<WordSummaryDto>(),
            Page = 1,
            PageSize = 40,
            TotalCount = 0,
            TotalPages = 0
        };
        _wordServiceMock
            .Setup(ws => ws.BrowseByLetterAsync(letter, 1, 40, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPage);

        // act
        var actionResult = await _sut.BrowseByLetter(letter, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedPage);
    }

    [Fact]
    public async Task BrowseByLetter_ShouldReturnBadRequest_WhenArgumentExceptionIsThrown()
    {
        // arrange
        var letter = "invalid";
        _wordServiceMock
            .Setup(ws => ws.BrowseByLetterAsync(letter, 1, 40, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.BrowseByLetter(letter, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnOk_WhenSubmittedIsTrue()
    {
        // arrange
        var id = "1";
        var request = new SubmitWordFeedbackRequestDto { FeedbackText = "Feed", CaptchaToken = "token" };
        _wordServiceMock
            .Setup(ws => ws.SubmitFeedbackAsync(id, "Feed", "token", "127.0.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.SubmitFeedback(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnNotFound_WhenSubmittedIsFalse()
    {
        // arrange
        var id = "1";
        var request = new SubmitWordFeedbackRequestDto { FeedbackText = "Feed", CaptchaToken = "token" };
        _wordServiceMock
            .Setup(ws => ws.SubmitFeedbackAsync(id, "Feed", "token", "127.0.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // act
        var actionResult = await _sut.SubmitFeedback(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnBadRequest_WhenArgumentExceptionIsThrown()
    {
        // arrange
        var id = "1";
        var request = new SubmitWordFeedbackRequestDto { FeedbackText = "Feed", CaptchaToken = "token" };
        _wordServiceMock
            .Setup(ws => ws.SubmitFeedbackAsync(id, "Feed", "token", "127.0.0.1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Bad Request"));

        // act
        var actionResult = await _sut.SubmitFeedback(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnOk_WhenSubmitted()
    {
        // arrange
        var request = new SubmitWordSuggestionRequestDto { Headword = "head", Definition = "def", Email = "test@test.com", CaptchaToken = "tok" };
        _wordServiceMock
            .Setup(ws => ws.SubmitSuggestionAsync("head", "def", "test@test.com", "tok", "127.0.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.SubmitSuggestion(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenArgumentExceptionIsThrown()
    {
        // arrange
        var request = new SubmitWordSuggestionRequestDto { Headword = "head", Definition = "def", Email = "test@test.com", CaptchaToken = "tok" };
        _wordServiceMock
            .Setup(ws => ws.SubmitSuggestionAsync("head", "def", "test@test.com", "tok", "127.0.0.1", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid"));

        // act
        var actionResult = await _sut.SubmitSuggestion(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }
}
