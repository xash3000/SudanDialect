using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Tests.Unit.Controllers;

public class AdminFeedbackControllerTests
{
    private readonly Mock<IAdminFeedbackService> _adminFeedbackServiceMock;
    private readonly AdminFeedbackController _sut;

    public AdminFeedbackControllerTests()
    {
        _adminFeedbackServiceMock = new Mock<IAdminFeedbackService>();
        _sut = new AdminFeedbackController(_adminFeedbackServiceMock.Object);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task GetPage_ShouldReturnOk_WithPageResults()
    {
        // arrange
        var query = new AdminFeedbackQueryDto();
        var expectedPage = new AdminFeedbackPageDto
        {
            Items = new List<AdminFeedbackItemDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        _adminFeedbackServiceMock
            .Setup(s => s.GetPageAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPage);

        // act
        var actionResult = await _sut.GetPage(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedPage);
    }

    [Fact]
    public async Task GetPage_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var query = new AdminFeedbackQueryDto();
        _adminFeedbackServiceMock
            .Setup(s => s.GetPageAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.GetPage(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SetResolved_ShouldReturnOk_WhenUpdated()
    {
        // arrange
        var id = 1;
        var request = new AdminSetFeedbackResolvedRequestDto { Resolved = true };

        _adminFeedbackServiceMock
            .Setup(s => s.SetResolvedAsync(id, request.Resolved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.SetResolved(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task SetResolved_ShouldReturnNotFound_WhenNotUpdated()
    {
        // arrange
        var id = 1;
        var request = new AdminSetFeedbackResolvedRequestDto { Resolved = true };

        _adminFeedbackServiceMock
            .Setup(s => s.SetResolvedAsync(id, request.Resolved, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // act
        var actionResult = await _sut.SetResolved(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task SetResolved_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var id = 1;
        var request = new AdminSetFeedbackResolvedRequestDto { Resolved = true };

        _adminFeedbackServiceMock
            .Setup(s => s.SetResolvedAsync(id, request.Resolved, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.SetResolved(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }
}
