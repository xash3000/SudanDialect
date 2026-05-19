using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Services;
using Xunit;

namespace SudanDialect.Tests.Unit.Services;

public class WordServiceTests
{
    private readonly Mock<IWordRepository> _wordRepositoryMock;
    private readonly Mock<IPublicIdEncoder> _publicIdEncoderMock;
    private readonly Mock<ITurnstileVerificationService> _turnstileVerificationServiceMock;
    private readonly IWordService _sut;

    public WordServiceTests()
    {
        _wordRepositoryMock = new Mock<IWordRepository>();
        _publicIdEncoderMock = new Mock<IPublicIdEncoder>();
        _turnstileVerificationServiceMock = new Mock<ITurnstileVerificationService>();

        _sut = new WordService(_wordRepositoryMock.Object,
                               _publicIdEncoderMock.Object,
                               _turnstileVerificationServiceMock.Object);
    }

    [Fact]
    public async Task GetByPublicIdAsync_ShouldReturnWordDetails_WhenWordExistsAndIdIsValid()
    {
        // arrange
        var publicId = "abcd1234";
        int decodedId = 1000;
        var expectedWord = new WordDetailsDto { Id = publicId, Headword = "تجربة", Definition = "معنى تجربة" };

        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId(publicId, out decodedId)).Returns(true);
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(decodedId)).Returns(publicId);
        _wordRepositoryMock.Setup(w => w.GetActiveByIdAsync(decodedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Word { Id = decodedId, Headword = expectedWord.Headword, Definition = expectedWord.Definition, IsActive = true });

        // act
        var returnedWord = await _sut.GetByPublicIdAsync(publicId, TestContext.Current.CancellationToken);

        // assert
        returnedWord.Should().NotBeNull();
        returnedWord.Should().BeEquivalentTo(expectedWord);
    }

    [Fact]
    public async Task GetByPublicIdAsync_ShouldReturnNull_WhenWordDoesNotExist()
    {
        // arrange
        var publicId = "abcd1234";
        int decodedId = 1000;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId(publicId, out decodedId)).Returns(true);
        _wordRepositoryMock.Setup(w => w.GetActiveByIdAsync(decodedId, It.IsAny<CancellationToken>())).ReturnsAsync((Word)null!);

        // act
        var returnedWord = await _sut.GetByPublicIdAsync(publicId, TestContext.Current.CancellationToken);

        // assert
        returnedWord.Should().BeNull();
    }

    [Fact]
    public async Task GetByPublicIdAsync_ShouldThrowArgumentException_WhenIdIsInvalid()
    {
        // arrange
        var publicId = "invalid";
        int decodedId = 0;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId(publicId, out decodedId)).Returns(false);

        // act & assert
        await _sut.Invoking(s => s.GetByPublicIdAsync(publicId, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmpty_WhenQueryIsNullOrWhiteSpace()
    {
        // act
        var results = await _sut.SearchAsync("   ", TestContext.Current.CancellationToken);

        // assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ShouldThrowArgumentException_WhenQueryIsTooLong()
    {
        // act & assert
        await _sut.Invoking(s => s.SearchAsync(new string('a', 201), TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnEmpty_WhenNormalizedQueryIsNullOrWhiteSpace()
    {
        // act
        // Use punctuation-only input which ArabicTextNormalizer.Normalize will reduce to empty
        var results2 = await _sut.SearchAsync("!!!", TestContext.Current.CancellationToken);

        // assert - normalized query is empty so repository should not be called and result is empty
        results2.Should().BeEmpty();
        _wordRepositoryMock.Verify(w => w.SearchActiveByNormalizedQueryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnResults_WhenValidQuery()
    {
        // arrange
        _wordRepositoryMock.Setup(w => w.SearchActiveByNormalizedQueryAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WordSearchCandidateDto> { new() { Id = 1, Headword = "تجربة" } });
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(1)).Returns("1");

        // act
        var results = await _sut.SearchAsync("تجربة", TestContext.Current.CancellationToken);

        // assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be("1");
    }

    [Theory]
    [MemberData(nameof(GetInvalidBrowseByLetterArguments))]
    public async Task BrowseByLetterAsync_ShouldThrowException_WhenArgumentsAreInvalid(string? letter, int page, int pageSize)
    {
        // act & assert
        await _sut.Invoking(s => s.BrowseByLetterAsync(letter, page, pageSize, TestContext.Current.CancellationToken))
            .Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task BrowseByLetterAsync_ShouldReturnEmpty_WhenLetterIsValidButNoResults()
    {
        // arrange
        _wordRepositoryMock.Setup(w => w.GetActiveByFirstLetterPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<Word>(), 0, 0, 1));

        // act
        var results = await _sut.BrowseByLetterAsync("ب", 1, 40, TestContext.Current.CancellationToken);

        // assert
        results.TotalCount.Should().Be(0);
        results.Items.Should().BeEmpty();
    }

    public static IEnumerable<object[]> GetInvalidFeedbackArguments()
    {
        yield return new object[] { "invalid", "text", "token" }; // decoding fails
        yield return new object[] { "1", "", "token" }; // text empty
        yield return new object[] { "1", new string('a', 2001), "token" }; // text too long
        yield return new object[] { "1", "text", "" }; // token empty
    }

    public static IEnumerable<object?[]> GetInvalidBrowseByLetterArguments()
    {
        const int maxBrowsePageSize = 60;

        yield return new object?[] { null, 1, 40 }; // null letter -> invalid input (letter required)
        yield return new object?[] { "ب", 0, 40 }; // invalid page -> page must be >= 1
        yield return new object?[] { "ب", 1, 0 }; // invalid pageSize -> pageSize must be >= 1
        yield return new object?[] { "ب", 1, maxBrowsePageSize + 1 }; // invalid pageSize -> exceeds maximum allowed
        yield return new object?[] { "بب", 1, 40 }; // invalid letter -> must be a single character
        yield return new object?[] { "a", 1, 40 }; // invalid letter -> must be an Arabic character
    }

    [Theory]
    [MemberData(nameof(GetInvalidFeedbackArguments))]
    public async Task SubmitFeedbackAsync_ShouldThrowException_WhenArgumentsInvalid(string publicId, string feedbackText, string captchaToken)
    {
        // arrange
        int decodedId = 1;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId("1", out decodedId)).Returns(true);

        // act & assert
        await _sut.Invoking(s => s.SubmitFeedbackAsync(publicId, feedbackText, captchaToken, "ip", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ShouldReturnFalse_WhenWordNotFound()
    {
        int decodedId = 1;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId("1", out decodedId)).Returns(true);
        _turnstileVerificationServiceMock.Setup(t => t.VerifyAsync("token", "ip", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _wordRepositoryMock.Setup(w => w.GetActiveByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Word)null!);

        var result = await _sut.SubmitFeedbackAsync("1", "text", "token", "ip", TestContext.Current.CancellationToken);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ShouldReturnTrue_WhenValid()
    {
        int decodedId = 1;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId("1", out decodedId)).Returns(true);
        _turnstileVerificationServiceMock.Setup(t => t.VerifyAsync("token", "ip", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _wordRepositoryMock.Setup(w => w.GetActiveByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new Word());

        var result = await _sut.SubmitFeedbackAsync("1", "text", "token", "ip", TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        _wordRepositoryMock.Verify(w => w.AddFeedbackAsync(It.IsAny<Feedback>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    public static IEnumerable<object[]> GetInvalidSuggestionArguments()
    {
        yield return new object[] { "", "def", "em", "tok" }; // empty headword
        yield return new object[] { "hw", "", "em", "tok" }; // empty definition
        yield return new object[] { new string('a', 201), "def", "em", "tok" }; // headword too long
        yield return new object[] { "hw", new string('a', 4001), "em", "tok" }; // def too long
        yield return new object[] { "hw", "def", "invalid-email", "tok" }; // email invalid
        yield return new object[] { "hw", "def", "test@test.com", "" }; // captcha empty
    }

    [Theory]
    [MemberData(nameof(GetInvalidSuggestionArguments))]
    public async Task SubmitSuggestionAsync_ShouldThrowException_WhenArgumentsInvalid(string headword, string definition, string email, string captchaToken)
    {
        // act & assert
        await _sut.Invoking(s => s.SubmitSuggestionAsync(headword, definition, email, captchaToken, "ip", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SubmitSuggestionAsync_ShouldReturnTrue_WhenValid()
    {
        _turnstileVerificationServiceMock.Setup(t => t.VerifyAsync("tok", "ip", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _sut.SubmitSuggestionAsync("hw", "def", "test@test.com", "tok", "ip", TestContext.Current.CancellationToken);
        result.Should().BeTrue();

        _wordRepositoryMock.Verify(w => w.AddSuggestionAsync(It.IsAny<WordSuggestion>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task BrowseByLetterAsync_ShouldReturnPagedResults_WhenValid()
    {
        // arrange
        var words = new List<Word>
        {
            new Word { Id = 2, Headword = "ا" } // DB returns sorted implicitly based on query skip/take
        };
        _wordRepositoryMock.Setup(w => w.GetActiveByFirstLetterPagedAsync("ا", "ا", 1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((words, 2, 2, 1));
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(1)).Returns("1");
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(2)).Returns("2");

        // act
        var results = await _sut.BrowseByLetterAsync("ا", 1, 1, TestContext.Current.CancellationToken);

        // assert
        results.TotalCount.Should().Be(2);
        results.TotalPages.Should().Be(2);
        results.Items.Should().HaveCount(1);
        results.Items[0].Id.Should().Be("2");
    }

    [Fact]
    public async Task BrowseByLetterAsync_ShouldReturnEmpty_WhenNormalizedLetterIsEmpty()
    {
        // act
        // Use Arabic Tatweel (U+0640) which Normalizer typically strips to empty.
        var page = 2;
        var pageSize = 10;
        var results = await _sut.BrowseByLetterAsync("\u0640", page, pageSize, TestContext.Current.CancellationToken);

        // assert - when normalized letter becomes empty the service returns an empty page preserving requested page/pageSize
        results.TotalCount.Should().Be(0);
        results.Items.Should().BeEmpty();
        results.Page.Should().Be(page);
        results.PageSize.Should().Be(pageSize);
        results.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task SubmitFeedbackAsync_ShouldThrowException_WhenCaptchaFails()
    {
        int decodedId = 1;
        _publicIdEncoderMock.Setup(p => p.TryDecodeWordId("1", out decodedId)).Returns(true);
        _turnstileVerificationServiceMock.Setup(t => t.VerifyAsync("token", "ip", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _sut.Invoking(s => s.SubmitFeedbackAsync("1", "text", "token", "ip", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*Captcha verification failed.*");
    }

    [Fact]
    public async Task SubmitSuggestionAsync_ShouldThrowException_WhenCaptchaFails()
    {
        _turnstileVerificationServiceMock.Setup(t => t.VerifyAsync("tok", "ip", It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await _sut.Invoking(s => s.SubmitSuggestionAsync("hw", "def", "test@test.com", "tok", "ip", TestContext.Current.CancellationToken))
            .Should().ThrowAsync<ArgumentException>().WithMessage("*Captcha verification failed.*");
    }

    [Fact]
    public async Task BrowseByLetterAsync_ShouldBoundPage_WhenPageExceedsTotal()
    {
        // arrange - two words and pageSize 1 -> totalPages = 2
        var words = new List<Word> { new Word { Id = 2, Headword = "ب" } }; // Page 2 item
        _wordRepositoryMock.Setup(w => w.GetActiveByFirstLetterPagedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((words, 2, 2, 2));
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(1)).Returns("1");
        _publicIdEncoderMock.Setup(p => p.EncodeWordId(2)).Returns("2");

        // act - request a page larger than total pages
        var results = await _sut.BrowseByLetterAsync("ا", 5, 1, TestContext.Current.CancellationToken);

        // assert - bounded to totalPages (2)
        results.TotalCount.Should().Be(2);
        results.TotalPages.Should().Be(2);
        results.Page.Should().Be(2);
    }

    [Fact]
    public async Task SubmitSuggestionAsync_ShouldThrowException_WhenEmailTooLong()
    {
        // arrange - craft an email longer than MaxSuggestionEmailLength (320)
        var longEmail = new string('a', 321);

        // act & assert
        await _sut.Invoking(s => s.SubmitSuggestionAsync("hw", "def", longEmail, "tok", "ip"))
            .Should().ThrowAsync<ArgumentException>();
    }
}
