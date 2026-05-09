using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using System.Security.Claims;

namespace SudanDialect.Tests.Unit.Controllers;

public class AdminWordsControllerTests
{
    private readonly Mock<IAdminWordService> _adminWordServiceMock;
    private readonly AdminWordsController _sut;

    public AdminWordsControllerTests()
    {
        _adminWordServiceMock = new Mock<IAdminWordService>();
        _sut = new AdminWordsController(_adminWordServiceMock.Object);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-id")
        }, "mock"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };
    }

    [Fact]
    public async Task GetMetrics_ShouldReturnOk_WithMetrics()
    {
        // arrange
        var expectedMetrics = new AdminDashboardMetricsDto { TotalWords = 100, ActiveWords = 90 };
        _adminWordServiceMock.Setup(s => s.GetMetricsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMetrics);

        // act
        var actionResult = await _sut.GetMetrics(TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedMetrics);
    }

    [Fact]
    public async Task GetPage_ShouldReturnOk_WithPage()
    {
        // arrange
        var query = new AdminWordTableQueryDto();
        var expectedPage = new WordPageDto
        {
            Items = new List<Word>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        _adminWordServiceMock.Setup(s => s.GetPageAsync(query, It.IsAny<CancellationToken>()))
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
    public async Task Create_ShouldReturnCreated_WithWord()
    {
        // arrange
        var request = new AdminCreateWordRequestDto { Headword = "test" };
        var createdWord = new Word { Id = 1, Headword = "test" };

        _adminWordServiceMock.Setup(s => s.CreateAsync(request, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWord);

        // act
        var actionResult = await _sut.Create(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var createdResult = actionResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        createdResult.Value.Should().BeEquivalentTo(createdWord);
    }

    [Fact]
    public async Task Update_ShouldReturnOk_WhenWordUpdated()
    {
        // arrange
        var id = 1;
        var request = new AdminUpdateWordRequestDto { Headword = "test" };
        var updatedWord = new Word { Id = 1, Headword = "test" };

        _adminWordServiceMock.Setup(s => s.UpdateAsync(id, request, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedWord);

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(updatedWord);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenAdminUserIdIsMissing()
    {
        // arrange
        var request = new AdminCreateWordRequestDto { Headword = "test" };
        _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // No NameIdentifier claim

        // act
        var actionResult = await _sut.Create(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var unauthorizedResult = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var request = new AdminCreateWordRequestDto { Headword = "test" };
        _adminWordServiceMock.Setup(s => s.CreateAsync(request, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.Create(request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenAdminUserIdIsMissing()
    {
        // arrange
        var id = 1;
        var request = new AdminUpdateWordRequestDto { Headword = "test" };
        _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // No NameIdentifier claim

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var unauthorizedResult = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenWordDoesNotExist()
    {
        // arrange
        var id = 1;
        var request = new AdminUpdateWordRequestDto { Headword = "test" };
        _adminWordServiceMock.Setup(s => s.UpdateAsync(id, request, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Word?)null);

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var id = 1;
        var request = new AdminUpdateWordRequestDto { Headword = "test" };
        _adminWordServiceMock.Setup(s => s.UpdateAsync(id, request, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.Update(id, request, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Deactivate_ShouldReturnUnauthorized_WhenAdminUserIdIsMissing()
    {
        // arrange
        var id = 1;
        _sut.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // No NameIdentifier claim

        // act
        var actionResult = await _sut.Deactivate(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var unauthorizedResult = actionResult.Result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        unauthorizedResult.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Deactivate_ShouldReturnNotFound_WhenWordDoesNotExist()
    {
        // arrange
        var id = 1;
        _adminWordServiceMock.Setup(s => s.DeactivateAsync(id, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // act
        var actionResult = await _sut.Deactivate(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Deactivate_ShouldReturnBadRequest_WhenArgumentOutOfRangeExceptionThrown()
    {
        // arrange
        var id = -1;
        _adminWordServiceMock.Setup(s => s.DeactivateAsync(id, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("id"));

        // act
        var actionResult = await _sut.Deactivate(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Deactivate_ShouldReturnOk_WhenDeactivated()
    {
        // arrange
        var id = 1;
        _adminWordServiceMock.Setup(s => s.DeactivateAsync(id, "admin-id", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var actionResult = await _sut.Deactivate(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task GetAuditPage_ShouldReturnOk_WithPage()
    {
        // arrange
        var query = new AdminWordEditAuditQueryDto();
        var expectedPage = new AdminWordEditAuditPageDto
        {
            Items = new List<AdminWordEditAuditEntryDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        _adminWordServiceMock.Setup(s => s.GetAuditPageAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPage);

        // act
        var actionResult = await _sut.GetAuditPage(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedPage);
    }

    [Fact]
    public async Task GetAuditPage_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var query = new AdminWordEditAuditQueryDto();
        _adminWordServiceMock.Setup(s => s.GetAuditPageAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.GetAuditPage(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetPage_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var query = new AdminWordTableQueryDto();
        _adminWordServiceMock.Setup(s => s.GetPageAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.GetPage(query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WithWord()
    {
        // arrange
        var id = 1;
        var expectedWord = new Word { Id = 1, Headword = "test" };
        _adminWordServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWord);

        // act
        var actionResult = await _sut.GetById(id, TestContext.Current.CancellationToken);

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
        var id = 1;
        _adminWordServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Word?)null);

        // act
        var actionResult = await _sut.GetById(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        actionResult.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetById_ShouldReturnBadRequest_WhenArgumentOutOfRangeExceptionThrown()
    {
        // arrange
        var id = -1;
        _adminWordServiceMock.Setup(s => s.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentOutOfRangeException("id"));

        // act
        var actionResult = await _sut.GetById(id, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAuditPageByWordId_ShouldReturnOk_WithPage()
    {
        // arrange
        var id = 1;
        var query = new AdminWordEditAuditQueryDto();
        var expectedPage = new AdminWordEditAuditPageDto
        {
            Items = new List<AdminWordEditAuditEntryDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        _adminWordServiceMock.Setup(s => s.GetAuditPageByWordIdAsync(id, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPage);

        // act
        var actionResult = await _sut.GetAuditPageByWordId(id, query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.StatusCode.Should().Be(200);
        okResult.Value.Should().BeEquivalentTo(expectedPage);
    }

    [Fact]
    public async Task GetAuditPageByWordId_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // arrange
        var id = 1;
        var query = new AdminWordEditAuditQueryDto();
        _adminWordServiceMock.Setup(s => s.GetAuditPageByWordIdAsync(id, query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid"));

        // act
        var actionResult = await _sut.GetAuditPageByWordId(id, query, TestContext.Current.CancellationToken);

        // assert
        actionResult.Result.Should().NotBeNull();
        var badRequestResult = actionResult.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.StatusCode.Should().Be(400);
    }
}
