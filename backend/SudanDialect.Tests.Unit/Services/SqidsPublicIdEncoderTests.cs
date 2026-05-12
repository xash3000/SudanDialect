using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Sqids;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Services;
using Xunit;

namespace SudanDialect.Tests.Unit.Services;

public class SqidsPublicIdEncoderTests
{
    private static SqidsPublicIdEncoder CreateSut(int minLength, string? signingKey)
    {
        var options = Options.Create(new PublicIdOptions { MinLength = minLength });

        var settings = new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = signingKey
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new SqidsPublicIdEncoder(options, configuration);
    }

    [Fact]
    public void Constructor_ShouldThrowInvalidOperationException_WhenJwtSigningKeyIsMissing()
    {
        var options = Options.Create(new PublicIdOptions { MinLength = 8 });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var act = () => new SqidsPublicIdEncoder(options, configuration);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrowInvalidOperationException_WhenJwtSigningKeyIsNullOrWhitespace(string? signingKey)
    {
        var act = () => CreateSut(minLength: 8, signingKey: signingKey);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EncodeWordId_ShouldThrowArgumentOutOfRangeException_WhenIdIsNotPositive(int id)
    {
        var sut = CreateSut(minLength: 8, signingKey: "test-signing-key");

        var act = () => sut.EncodeWordId(id);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EncodeWordIdAndTryDecodeWordId_ShouldRoundTrip_WhenValid()
    {
        var sut = CreateSut(minLength: 8, signingKey: "test-signing-key");

        var encoded = sut.EncodeWordId(123);
        encoded.Should().NotBeNullOrWhiteSpace();

        sut.TryDecodeWordId(encoded, out var decodedId).Should().BeTrue();
        decodedId.Should().Be(123);

        sut.TryDecodeWordId($"  {encoded}  ", out var decodedTrimmed).Should().BeTrue();
        decodedTrimmed.Should().Be(123);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    public void TryDecodeWordId_ShouldReturnFalse_WhenEncodedIdIsInvalid(string? encodedId)
    {
        var sut = CreateSut(minLength: 8, signingKey: "test-signing-key");

        var result = sut.TryDecodeWordId(encodedId!, out var decodedId);

        result.Should().BeFalse();
        decodedId.Should().Be(0);
    }

    [Fact]
    public void TryDecodeWordId_ShouldReturnFalse_WhenDecodedCandidateIsNotPositive()
    {
        const string signingKey = "test-signing-key";

        var sut = CreateSut(minLength: 0, signingKey: signingKey);

        var alphabet = SqidsPublicIdEncoder.BuildAlphabetFromSigningKey(signingKey);
        var encoder = new SqidsEncoder<int>(new SqidsOptions { MinLength = 0, Alphabet = alphabet });
        var encodedZero = encoder.Encode(0);
        encodedZero.Should().NotBeNullOrWhiteSpace();

        var result = sut.TryDecodeWordId(encodedZero, out var decodedId);

        result.Should().BeFalse();
        decodedId.Should().Be(0);
    }

    [Fact]
    public void EncodeWordId_ShouldRespectConfiguredMinLength_WhenMinLengthIsPositive()
    {
        var sut = CreateSut(minLength: 16, signingKey: "test-signing-key");

        var encoded = sut.EncodeWordId(123);

        encoded.Length.Should().BeGreaterThanOrEqualTo(16);
    }

    [Fact]
    public void Constructor_ShouldTreatNegativeMinLengthAsZero()
    {
        const string signingKey = "test-signing-key";

        var sutWithNegativeMinLength = CreateSut(minLength: -5, signingKey: signingKey);
        var sutWithZeroMinLength = CreateSut(minLength: 0, signingKey: signingKey);

        var encoded1 = sutWithNegativeMinLength.EncodeWordId(123);
        var encoded2 = sutWithZeroMinLength.EncodeWordId(123);

        encoded1.Should().Be(encoded2);
    }

    [Fact]
    public void EncodeWordId_ShouldUseBase62CharactersOnly()
    {
        var sut = CreateSut(minLength: 8, signingKey: "test-signing-key");

        var encoded = sut.EncodeWordId(123);

        encoded.Should().MatchRegex("^[a-zA-Z0-9]+$");
    }
}
