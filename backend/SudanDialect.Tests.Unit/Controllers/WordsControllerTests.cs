using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualBasic;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Services;

namespace SudanDialect.Tests.Unit.Controllers;

public class WordsControllerTests
{
    private readonly Mock<IWordService> _wordServiceMock;
    private readonly WordsController _sut;

    public WordsControllerTests()
    {
        _wordServiceMock = new Mock<IWordService>();
        _sut = new WordsController(_wordServiceMock.Object);
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
        var actionResult = await _sut.GetById(publicId, CancellationToken.None);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedWord);
    }
}
