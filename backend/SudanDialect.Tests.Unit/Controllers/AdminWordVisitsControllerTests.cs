using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SudanDialect.Api.Controllers;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Tests.Unit.Controllers;

public class AdminWordVisitsControllerTests
{
    private readonly Mock<IAdminWordVisitService> _adminWordVisitServiceMock;
    private readonly AdminWordVisitsController _sut;

    public AdminWordVisitsControllerTests()
    {
        _adminWordVisitServiceMock = new Mock<IAdminWordVisitService>();
        _sut = new AdminWordVisitsController(_adminWordVisitServiceMock.Object);
    }

    [Fact]
    public async Task GetVisits_ShouldReturnOk_WithPage()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto { Page = 1, PageSize = 10 };
        var expectedPage = new AdminWordVisitPageDto
        {
            Items = new List<AdminWordVisitStatsDto>(),
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPages = 0
        };

        _adminWordVisitServiceMock.Setup(s => s.GetWordVisitsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPage);

        // Act
        var result = await _sut.GetVisits(query, TestContext.Current.CancellationToken);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedPage);
    }

    [Fact]
    public async Task GetVisits_ShouldReturnBadRequest_WhenArgumentExceptionThrown()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto();
        _adminWordVisitServiceMock.Setup(s => s.GetWordVisitsAsync(query, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid query"));

        // Act
        var result = await _sut.GetVisits(query, TestContext.Current.CancellationToken);

        // Assert
        var badRequestResult = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().BeEquivalentTo(new { error = "Invalid query" });
    }
}
