using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Services;

namespace SudanDialect.Tests.Unit.Services;

public class AdminWordSuggestionServiceTests
{
    private readonly Mock<IAdminWordSuggestionRepository> _repositoryMock;
    private readonly IAdminWordSuggestionService _sut;

    public AdminWordSuggestionServiceTests()
    {
        _repositoryMock = new Mock<IAdminWordSuggestionRepository>();
        _sut = new AdminWordSuggestionService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetPageAsync_ShouldReturnPagedResults()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = 1, PageSize = 10 };
        var items = new List<AdminWordSuggestionItemDto> { new() { Id = 1, Headword = "Test" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldClampPageAndPageSize()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = -1, PageSize = 500 };
        var items = new List<AdminWordSuggestionItemDto> { new() { Id = 1, Headword = "Test" } };
        // MaxPageSize is 200, Page should become 1
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, true, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(200);
    }

    [Fact]
    public async Task GetPageAsync_ShouldHandleZeroPageAndPageSize()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = 0, PageSize = 0 };
        var items = new List<AdminWordSuggestionItemDto> { new() { Id = 1, Headword = "Test" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, true, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetPageAsync_ShouldHandleZeroTotalCountAndLowerCaseAsc()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = 2, PageSize = 10, SortDirection = "asc" };
        var items = new List<AdminWordSuggestionItemDto>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, false, 2, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.TotalPages.Should().Be(0);
        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldHandleAscendingSort()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = 1, PageSize = 10, SortDirection = "asc" };
        var items = new List<AdminWordSuggestionItemDto> { new() { Id = 1, Headword = "Test" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldReQuery_WhenPageGreaterThanTotalPages()
    {
        // arrange
        var query = new AdminWordSuggestionQueryDto { Page = 5, PageSize = 10 };
        var items = new List<AdminWordSuggestionItemDto>();
        // First call returning 10 items total -> 1 page.
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, true, 5, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 10));

        var items2 = new List<AdminWordSuggestionItemDto> { new() { Id = 1, Headword = "Test" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, null, true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items2, 10));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Page.Should().Be(1);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task SetResolvedAsync_ShouldReturnTrue_WhenSuccessful()
    {
        // arrange
        _repositoryMock.Setup(r => r.SetResolvedAsync(1, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var result = await _sut.SetResolvedAsync(1, true, TestContext.Current.CancellationToken);

        // assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetResolvedAsync_ShouldThrowException_WhenIdIsInvalid()
    {
        // act & assert
        await _sut.Invoking(s => s.SetResolvedAsync(0, true))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
