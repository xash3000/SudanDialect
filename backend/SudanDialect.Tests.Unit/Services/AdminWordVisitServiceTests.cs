using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Models;
using SudanDialect.Api.Services;

namespace SudanDialect.Tests.Unit.Services;

public class AdminWordVisitServiceTests
{
    private readonly Mock<IAdminWordRepository> _adminWordRepositoryMock;
    private readonly AdminWordVisitService _sut;

    public AdminWordVisitServiceTests()
    {
        _adminWordRepositoryMock = new Mock<IAdminWordRepository>();
        _sut = new AdminWordVisitService(_adminWordRepositoryMock.Object);
    }

    [Fact]
    public async Task GetWordVisitsAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto { Page = 1, PageSize = 10, SortBy = "visitCount", SortDirection = "desc" };
        var items = new List<Word>
        {
            new() { Id = 1, Headword = "تجربة", VisitCount = 100, LastVisitedAt = DateTime.UtcNow }
        };
        _adminWordRepositoryMock.Setup(r => r.GetVisitsPagedAsync("visitCount", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // Act
        var result = await _sut.GetWordVisitsAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(1);
        
        var firstItem = result.Items[0];
        firstItem.Id.Should().Be(1);
        firstItem.Headword.Should().Be("تجربة");
        firstItem.VisitCount.Should().Be(100);
    }

    [Fact]
    public async Task GetWordVisitsAsync_ShouldClampPageSize_AndEnsureMinPage()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto { Page = -5, PageSize = 500 };
        _adminWordRepositoryMock.Setup(r => r.GetVisitsPagedAsync(It.IsAny<string>(), It.IsAny<bool>(), 1, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Word>(), 0));

        // Act
        var result = await _sut.GetWordVisitsAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(100);
    }

    [Fact]
    public async Task GetWordVisitsAsync_ShouldUseDefaultSort_WhenNotProvided()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto();
        _adminWordRepositoryMock.Setup(r => r.GetVisitsPagedAsync("visitCount", true, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Word>(), 0));

        // Act
        await _sut.GetWordVisitsAsync(query, TestContext.Current.CancellationToken);

        // Assert
        _adminWordRepositoryMock.Verify(r => r.GetVisitsPagedAsync("visitCount", true, 1, 20, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetWordVisitsAsync_ShouldSetTotalPagesToZero_WhenNoItems()
    {
        // Arrange
        var query = new AdminWordVisitQueryDto();
        _adminWordRepositoryMock.Setup(r => r.GetVisitsPagedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Word>(), 0));

        // Act
        var result = await _sut.GetWordVisitsAsync(query, TestContext.Current.CancellationToken);

        // Assert
        result.TotalPages.Should().Be(0);
    }
}
