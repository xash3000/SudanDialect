using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Services;
using SudanDialect.Api.Utilities;

namespace SudanDialect.Tests.Unit.Services;

public class AdminWordServiceTests
{
    private readonly Mock<IAdminWordRepository> _repositoryMock;
    private readonly AdminWordService _sut;

    public AdminWordServiceTests()
    {
        _repositoryMock = new Mock<IAdminWordRepository>();
        _sut = new AdminWordService(_repositoryMock.Object);
    }

    #region GetMetricsAsync Tests

    [Fact]
    public async Task GetMetricsAsync_ShouldReturnMetrics()
    {
        var expected = new AdminDashboardMetricsDto { TotalWords = 10 };
        _repositoryMock.Setup(r => r.GetMetricsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetMetricsAsync(TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region GetPageAsync Tests

    [Fact]
    public async Task GetPageAsync_ShouldReturnPagedResults()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10 };
        var items = new List<Word> { new() { Id = 1, Headword = "تجربة" } };
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 1));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldUseDefaultPage_WhenPageIsZeroOrNegative()
    {
        var query = new AdminWordTableQueryDto { Page = 0, PageSize = 10 };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.Page.Should().Be(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldUseDefaultPageSize_WhenPageSizeIsZeroOrNegative()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 0 };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetPageAsync_ShouldCapPageSizeAt200()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 500 };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.PageSize.Should().Be(200);
    }

    [Fact]
    public async Task GetPageAsync_ShouldThrow_WhenFilterLengthExceeds200()
    {
        var query = new AdminWordTableQueryDto { Query = new string('a', 201) };

        var act = () => _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public async Task GetPageAsync_ShouldThrow_WhenIdFilterIsNotNumeric()
    {
        var query = new AdminWordTableQueryDto { Query = "abc", SearchBy = "id" };

        var act = () => _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task GetPageAsync_ShouldThrow_WhenIdFilterIsZeroOrNegative()
    {
        var query = new AdminWordTableQueryDto { Query = "0", SearchBy = "id" };

        var act = () => _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task GetPageAsync_ShouldReFetch_WhenPageExceedsTotalPages()
    {
        var query = new AdminWordTableQueryDto { Page = 10, PageSize = 10 };
        var items = new List<Word> { new() { Id = 1 } };
        _repositoryMock.SetupSequence(r => r.GetPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 100))
            .ReturnsAsync((items, 100));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.Page.Should().Be(10);
        _repositoryMock.Verify(r => r.GetPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<bool>(), 10, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldNormalizeSortByField()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = "HEADWORD" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "headword", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "headword", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldDefaultToUpdatedAtSort()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = "invalid" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldNormalizeSortDirectionToAsc()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortDirection = "ASC" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, It.IsAny<string>(), false, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldDefaultSortDirectionToDesc()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortDirection = "invalid" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, It.IsAny<string>(), true, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldDefaultSearchByToHeadword()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SearchBy = "invalid" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, It.IsAny<bool>(), null, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ShouldThrow_WhenIdIsZeroOrNegative()
    {
        var act = () => _sut.GetByIdAsync(0, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldThrow_WhenIdIsNegative()
    {
        var act = () => _sut.GetByIdAsync(-1, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnWord_WhenValid()
    {
        var expected = new Word { Id = 1, Headword = "تجربة" };
        _repositoryMock.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _sut.GetByIdAsync(1, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordIsNotArabic()
    {
        var request = new AdminCreateWordRequestDto { Headword = "English", Definition = "تجربة" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Arabic characters*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenDefinitionIsNotArabic()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "English" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Arabic characters*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordIsNull()
    {
        var request = new AdminCreateWordRequestDto { Headword = null!, Definition = "تجربة" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordIsEmpty()
    {
        var request = new AdminCreateWordRequestDto { Headword = "", Definition = "تجربة" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordIsWhitespace()
    {
        var request = new AdminCreateWordRequestDto { Headword = "   ", Definition = "تجربة" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenHeadwordExceeds200Chars()
    {
        var headword = new string('ت', 201);
        var request = new AdminCreateWordRequestDto { Headword = headword, Definition = "تجربة" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*200*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenDefinitionExceeds4000Chars()
    {
        var definition = new string('ت', 4001);
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = definition };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*4000*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenAdminUserIdIsNull()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "تعريف" };

        var act = () => _sut.CreateAsync(request, null!, "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrowException_WhenAdminUserIdIsWhitespace()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "تعريف" };

        var act = () => _sut.CreateAsync(request, "   ", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnWord_WhenValid()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "تعريف" };
        var expected = new Word { Id = 1, Headword = "تجربة" };
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Word>(), "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task CreateAsync_ShouldTrimInputs()
    {
        var request = new AdminCreateWordRequestDto { Headword = "  تجربة  ", Definition = "  تعريف  " };
        var expected = new Word { Id = 1, Headword = "تجربة" };
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<Word>(), "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenIdIsZeroOrNegative()
    {
        var request = new AdminUpdateWordRequestDto { Headword = "تجربة", Definition = "تعريف", IsActive = true };

        var act = () => _sut.UpdateAsync(0, request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenAdminUserIdIsNull()
    {
        var request = new AdminUpdateWordRequestDto { Headword = "تجربة", Definition = "تعريف", IsActive = true };

        var act = () => _sut.UpdateAsync(1, request, null!, "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenAdminUserIdIsWhitespace()
    {
        var request = new AdminUpdateWordRequestDto { Headword = "تجربة", Definition = "تعريف", IsActive = true };

        var act = () => _sut.UpdateAsync(1, request, "   ", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenHeadwordIsNotArabic()
    {
        var request = new AdminUpdateWordRequestDto { Headword = "English", Definition = "تجربة", IsActive = true };

        var act = () => _sut.UpdateAsync(1, request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Arabic characters*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldCallRepository_WhenValid()
    {
        var request = new AdminUpdateWordRequestDto { Headword = "تجربة", Definition = "تعريف", IsActive = true };
        var expected = new Word { Id = 1, Headword = "تجربة" };
        _repositoryMock.Setup(r => r.UpdateAsync(1, "تجربة", It.IsAny<string>(), "تعريف", It.IsAny<string>(), true, "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.UpdateAsync(1, request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region DeactivateAsync Tests

    [Fact]
    public async Task DeactivateAsync_ShouldReturnTrue_WhenSuccessful()
    {
        _repositoryMock.Setup(r => r.SetInactiveAsync(1, "admin", "ip", "ua", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeactivateAsync(1, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_ShouldThrow_WhenIdIsZeroOrNegative()
    {
        var act = () => _sut.DeactivateAsync(0, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task DeactivateAsync_ShouldThrow_WhenAdminUserIdIsNull()
    {
        var act = () => _sut.DeactivateAsync(1, null!, "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task DeactivateAsync_ShouldThrow_WhenAdminUserIdIsWhitespace()
    {
        var act = () => _sut.DeactivateAsync(1, "   ", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    #endregion

    #region GetAuditPageAsync Tests

    [Fact]
    public async Task GetAuditPageAsync_ShouldReturnPagedResults()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 10 };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, null, false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAuditPageAsync_ShouldNormalizeQueryParameters()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 0, PageSize = 0, SortDirection = "ASC" };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, null, false, 1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetAuditPageAsync_ShouldCapPageSizeAt200()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 500 };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, null, false, 1, 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        result.PageSize.Should().Be(200);
    }

[Fact]
    public async Task GetAuditPageAsync_ShouldBoundedPage_WhenPageExceedsTotalPages()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 15, PageSize = 10 };
        int? capturedPage = null;
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, null, true, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<int?, string?, bool, int, int, CancellationToken>((_, _, _, page, _, _) => capturedPage = page)
            .ReturnsAsync((new List<AdminWordEditAuditEntryDto>(), 20));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        capturedPage.Should().Be(2);
        result.Page.Should().Be(2);
    }

    #endregion

    #region GetAuditPageByWordIdAsync Tests

    [Fact]
    public async Task GetAuditPageByWordIdAsync_ShouldThrow_WhenWordIdIsZeroOrNegative()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 10 };

        var act = () => _sut.GetAuditPageByWordIdAsync(0, query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithMessage("*positive integer*");
    }

    [Fact]
    public async Task GetAuditPageByWordIdAsync_ShouldCallRepository_WhenValid()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 10 };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(1, null, false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageByWordIdAsync(1, query, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    #endregion

    #region NormalizeAuditActionType Tests

    [Fact]
    public async Task GetAuditPageAsync_ShouldThrow_WhenActionTypeIsInvalid()
    {
        var query = new AdminWordEditAuditQueryDto { ActionType = "invalid" };

        var act = () => _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*actionType*");
    }

    [Fact]
    public async Task GetAuditPageAsync_ShouldNormalizeActionType()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 10, ActionType = "CREATE" };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, "create", false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAuditPageAsync_ShouldAllowNullActionType()
    {
        var query = new AdminWordEditAuditQueryDto { Page = 1, PageSize = 10, ActionType = null };
        var items = new List<AdminWordEditAuditEntryDto>();
        _repositoryMock.Setup(r => r.GetAuditPagedAsync(null, null, false, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        var result = await _sut.GetAuditPageAsync(query, TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
    }

    #endregion

    #region ValidateArabicText Tests

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenDefinitionIsNull()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = null! };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenDefinitionIsEmpty()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "" };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenDefinitionIsWhitespace()
    {
        var request = new AdminCreateWordRequestDto { Headword = "تجربة", Definition = "   " };

        var act = () => _sut.CreateAsync(request, "admin", "ip", "ua", TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*required*");
    }

    #endregion

    #region NormalizeSortBy Tests

    [Fact]
    public async Task GetPageAsync_ShouldSupportIdSort()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = "id" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "id", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "id", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldSupportCreatedAtSort()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = "createdat" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "createdat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "createdat", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldSupportIsActiveSort()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = "isactive" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "isactive", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "isactive", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region IsActive Filter Tests

    [Fact]
    public async Task GetPageAsync_ShouldFilterByIsActive_True()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, IsActive = true };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, true, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, true, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldFilterByIsActive_False()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, IsActive = false };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, false, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, false, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldUseIdSearchBy()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, Query = "1", SearchBy = "id" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync("1", "", 1, false, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync("1", "", 1, false, null, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldReFetchWithBoundedPage_WhenPageExceedsTotalPages()
    {
        var query = new AdminWordTableQueryDto { Page = 15, PageSize = 10 };
        var items = new List<Word> { new() { Id = 1 } };
        _repositoryMock.SetupSequence(r => r.GetPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 50))
            .ReturnsAsync((items, 50));

        var result = await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        result.Page.Should().Be(5);
        _repositoryMock.Verify(r => r.GetPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<bool>(), It.IsAny<bool?>(), It.IsAny<string>(), It.IsAny<bool>(), 5, 10, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldPassEmptyNormalizedFilter_WhenIdSearch()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, Query = "123", SearchBy = "id" };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync("123", "", 123, false, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync("123", "", 123, false, null, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldDefaultSortBy_WhenNull()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SortBy = null };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPageAsync_ShouldDefaultSearchBy_WhenNull()
    {
        var query = new AdminWordTableQueryDto { Page = 1, PageSize = 10, SearchBy = null };
        var items = new List<Word>();
        _repositoryMock.Setup(r => r.GetPagedAsync(null, string.Empty, null, true, null, "updatedat", true, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 0));

        await _sut.GetPageAsync(query, TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.GetPagedAsync(null, string.Empty, null, true, null, It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}