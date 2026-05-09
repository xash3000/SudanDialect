using FluentAssertions;
using Moq;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Services;
using System.ComponentModel.DataAnnotations;

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
        var expectedWord = new WordDetailsDto
        {
            Id = publicId,
            Headword = "تجربة",
            Definition = "معنى تجربة"
        };

        _publicIdEncoderMock
            .Setup(publicIdEncoder => publicIdEncoder.TryDecodeWordId(publicId, out decodedId))
            .Returns(true);

        _publicIdEncoderMock
            .Setup(publicIdEncoder => publicIdEncoder.EncodeWordId(decodedId))
            .Returns(publicId);

        _wordRepositoryMock
            .Setup(wordRepository => wordRepository.GetActiveByIdAsync(decodedId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Word
            {
                Id = decodedId,
                Headword = expectedWord.Headword,
                Definition = expectedWord.Definition,
                NormalizedHeadword = expectedWord.Headword,
                NormalizedDefinition = expectedWord.Definition,
                IsActive = true,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });

        // act
        var returnedWord = await _sut.GetByPublicIdAsync(publicId, CancellationToken.None);

        // assert
        returnedWord.Should().NotBeNull();
        returnedWord.Should().BeEquivalentTo(expectedWord);
    }
}
