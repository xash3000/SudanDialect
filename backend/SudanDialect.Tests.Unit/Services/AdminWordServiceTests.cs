using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Services;

namespace SudanDialect.Tests.Unit.Services;

public class AdminWordServiceTests
{
    private readonly Mock<IAdminWordRepository> _repositoryMock;
    private readonly IAdminWordService _sut;

    public AdminWordServiceTests()
    {
        _repositoryMock = new Mock<IAdminWordRepository>();
        _sut = new AdminWordService(_repositoryMock.Object);
    }

    [Fact]
    public async Task GetMetricsAsync_ShouldReturnMetrics()
    {
        // arrange
        var expected = new AdminDashboardMetricsDto { TotalWords = 10 };
        _repositoryMock.Setup(r => r.GetMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        // act
        var result = await _sut.GetMetricsAsync(TestContext.Current.CancellationToken);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GetPageAsync_ShouldReturnPagedResults()
    {
        // arrange
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10 };
        var items = new List<Word> { new() { Id = 1, Headword = "تجربة" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        // act
        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        // assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordIsNotArabic()
    {
        // arrange
        var request = new AdminCreateWordRequestDto { Headword = "English", Definition = "تجربة" };

        // act & assert
        await _sut.Invoking(s => s.CreateAsync(request, "admin", "ip", "ua"))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*Arabic characters*");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnWord_WhenValid()
    {
        // arrange
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "تجربة" };
        var expected = new Word { Id = 1, Headword = "تجربة" };
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Word>(), "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // act
        var result = await _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        // assert
        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldReturnTrue_WhenSuccessful()
    {
        // arrange
        _repositoryMock.Setup(r => r.SetInactiveAsync(1, "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // act
        var result = await _sut.DeactivateAsync(1, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        // assert
        result.Should().BeTrue();
    }
}
