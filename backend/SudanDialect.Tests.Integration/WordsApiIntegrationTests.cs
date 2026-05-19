using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Utilities;
using Testcontainers.PostgreSql;
using Xunit;

namespace SudanDialect.Tests.Integration;

public sealed class WordsApiIntegrationTests : IAsyncLifetime
{
    private const string JwtSigningKey = "integration-test-signing-key-please-change-if-needed";

    private const string DefaultConnectionEnvVarName = "ConnectionStrings__DefaultConnection";
    private const string JwtSigningKeyEnvVarName = "Jwt__SigningKey";
    private const string PublicIdMinLengthEnvVarName = "PublicId__MinLength";

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase($"sudandialect_test_{Guid.NewGuid():N}")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(5432))
        .Build();

    private SudanDialectApiFactory? _factory;
    private HttpClient? _client;

    private string? _previousDefaultConnectionString;
    private string? _previousJwtSigningKey;
    private string? _previousPublicIdMinLength;

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        var connectionString = _dbContainer.GetConnectionString();

        await ApplyMigrationsAsync(connectionString);

        // Ensure the API reads *our* container connection string at startup (before Program.cs runs).
        _previousDefaultConnectionString = Environment.GetEnvironmentVariable(DefaultConnectionEnvVarName);
        _previousJwtSigningKey = Environment.GetEnvironmentVariable(JwtSigningKeyEnvVarName);
        _previousPublicIdMinLength = Environment.GetEnvironmentVariable(PublicIdMinLengthEnvVarName);

        Environment.SetEnvironmentVariable(DefaultConnectionEnvVarName, connectionString);
        Environment.SetEnvironmentVariable(JwtSigningKeyEnvVarName, JwtSigningKey);
        Environment.SetEnvironmentVariable(PublicIdMinLengthEnvVarName, "8");

        _factory = new SudanDialectApiFactory(connectionString, JwtSigningKey);
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _dbContainer.DisposeAsync().AsTask();

        Environment.SetEnvironmentVariable(DefaultConnectionEnvVarName, _previousDefaultConnectionString);
        Environment.SetEnvironmentVariable(JwtSigningKeyEnvVarName, _previousJwtSigningKey);
        Environment.SetEnvironmentVariable(PublicIdMinLengthEnvVarName, _previousPublicIdMinLength);
    }

    [Fact]
    public async Task Search_ShouldReturnWord_WhenQueryMatchesHeadword()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var headword = "تجربة";
        var definition = "معنى تجربة";

        var word = new Word
        {
            Headword = headword,
            NormalizedHeadword = ArabicTextNormalizer.Normalize(headword),
            Definition = definition,
            NormalizedDefinition = ArabicTextNormalizer.Normalize(definition),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        using var searchResponse = await _client!.GetAsync(
            $"/api/words/search?query={Uri.EscapeDataString(headword)}",
            TestContext.Current.CancellationToken);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<WordSearchResultDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        searchResults.Should().NotBeNull();
        searchResults!.Should().Contain(result =>
            result.Id == publicId && result.Headword == headword && result.SimilarityScore > 0);
    }

    [Fact]
    public async Task GetById_ShouldReturnWordDetails_WhenValidPublicIdProvided()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var headword = "تجربة";
        var definition = "معنى تجربة";

        var word = new Word
        {
            Headword = headword,
            NormalizedHeadword = ArabicTextNormalizer.Normalize(headword),
            Definition = definition,
            NormalizedDefinition = ArabicTextNormalizer.Normalize(definition),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        var details = await _client!.GetFromJsonAsync<WordDetailsDto>(
            $"/api/words/{publicId}",
            cancellationToken: TestContext.Current.CancellationToken);

        details.Should().NotBeNull();
        details!.Id.Should().Be(publicId);
        details.Headword.Should().Be(headword);
        details.Definition.Should().Be(definition);
    }


    [Fact]
    public async Task Browse_ShouldReturnWordsByLetter_WhenValidLetterProvided()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var headword = "تجربة";
        var definition = "معنى تجربة";

        var word = new Word
        {
            Headword = headword,
            NormalizedHeadword = ArabicTextNormalizer.Normalize(headword),
            Definition = definition,
            NormalizedDefinition = ArabicTextNormalizer.Normalize(definition),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        var letter = headword[..1];
        var browse = await _client!.GetFromJsonAsync<WordBrowsePageDto>(
            $"/api/words/browse?letter={Uri.EscapeDataString(letter)}&page=1&pageSize=40",
            cancellationToken: TestContext.Current.CancellationToken);

        browse.Should().NotBeNull();
        browse!.Items.Should().Contain(item => item.Id == publicId && item.Headword == headword);
    }

    [Fact]
    public async Task Search_ShouldReturnEmptyArray_WhenQueryIsNull()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/search",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<List<WordSearchResultDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ShouldReturnEmptyArray_WhenQueryIsEmpty()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/search?query=",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await response.Content.ReadFromJsonAsync<List<WordSearchResultDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        results.Should().NotBeNull();
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_ShouldReturnBadRequest_WhenQueryExceedsMaxLength()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        var longQuery = new string('ت', 202);

        using var response = await _client!.GetAsync(
            $"/api/words/search?query={Uri.EscapeDataString(longQuery)}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_ShouldReturnBadRequest_WhenLetterIsNotArabic()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/browse?letter=a&page=1&pageSize=40",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_ShouldReturnBadRequest_WhenLetterHasMultipleCharacters()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/browse?letter=تج&page=1&pageSize=40",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_ShouldReturnBadRequest_WhenPageIsZero()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/browse?letter=ت&page=0&pageSize=40",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Browse_ShouldReturnBadRequest_WhenPageSizeExceedsMax()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/browse?letter=ت&page=1&pageSize=100",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ShouldReturnBadRequest_WhenPublicIdIsInvalid()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.GetAsync(
            "/api/words/invalid-id",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenWordDoesNotExist()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var nonexistentId = publicIdEncoder.EncodeWordId(99999);

        using var response = await _client!.GetAsync(
            $"/api/words/{nonexistentId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenWordIsInactive()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var word = new Word
        {
            Headword = "غير نشط",
            NormalizedHeadword = ArabicTextNormalizer.Normalize("غير نشط"),
            Definition = "تعريف",
            NormalizedDefinition = ArabicTextNormalizer.Normalize("تعريف"),
            IsActive = false
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        using var response = await _client!.GetAsync(
            $"/api/words/{publicId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnOk_WhenValidFeedbackProvided()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var word = new Word
        {
            Headword = "تجربة",
            NormalizedHeadword = ArabicTextNormalizer.Normalize("تجربة"),
            Definition = "معنى تجربة",
            NormalizedDefinition = ArabicTextNormalizer.Normalize("معنى تجربة"),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        using var response = await _client!.PostAsync(
            $"/api/words/{publicId}/feedback",
            JsonContent.Create(new { feedbackText = "Test feedback", captchaToken = "valid-token" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnNotFound_WhenWordDoesNotExist()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var nonexistentId = publicIdEncoder.EncodeWordId(99999);

        using var response = await _client!.PostAsync(
            $"/api/words/{nonexistentId}/feedback",
            JsonContent.Create(new { feedbackText = "Test feedback", captchaToken = "valid-token" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnBadRequest_WhenFeedbackTextIsEmpty()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var word = new Word
        {
            Headword = "تجربة",
            NormalizedHeadword = ArabicTextNormalizer.Normalize("تجربة"),
            Definition = "معنى تجربة",
            NormalizedDefinition = ArabicTextNormalizer.Normalize("معنى تجربة"),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        using var response = await _client!.PostAsync(
            $"/api/words/{publicId}/feedback",
            JsonContent.Create(new { feedbackText = "", captchaToken = "valid-token" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnBadRequest_WhenFeedbackTextExceedsMaxLength()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var word = new Word
        {
            Headword = "تجربة",
            NormalizedHeadword = ArabicTextNormalizer.Normalize("تجربة"),
            Definition = "معنى تجربة",
            NormalizedDefinition = ArabicTextNormalizer.Normalize("معنى تجربة"),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);
        var longFeedback = new string('ت', 2001);

        using var response = await _client!.PostAsync(
            $"/api/words/{publicId}/feedback",
            JsonContent.Create(new { feedbackText = longFeedback, captchaToken = "valid-token" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitFeedback_ShouldReturnBadRequest_WhenCaptchaTokenIsMissing()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publicIdEncoder = scope.ServiceProvider.GetRequiredService<IPublicIdEncoder>();

        var word = new Word
        {
            Headword = "تجربة",
            NormalizedHeadword = ArabicTextNormalizer.Normalize("تجربة"),
            Definition = "معنى تجربة",
            NormalizedDefinition = ArabicTextNormalizer.Normalize("معنى تجربة"),
            IsActive = true
        };

        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var publicId = publicIdEncoder.EncodeWordId(word.Id);

        using var response = await _client!.PostAsync(
            $"/api/words/{publicId}/feedback",
            JsonContent.Create(new { feedbackText = "Test feedback" }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnOk_WhenValidSuggestionProvided()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة جديدة",
                definition = "تعريف جديد",
                email = "test@example.com",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnOk_WhenEmailIsOptional()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة جديدة",
                definition = "تعريف جديد",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenHeadwordIsMissing()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                definition = "تعريف جديد",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenDefinitionIsMissing()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة جديدة",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenHeadwordExceedsMaxLength()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        var longHeadword = new string('ت', 201);

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = longHeadword,
                definition = "تعريف",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenDefinitionExceedsMaxLength()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        var longDefinition = new string('ت', 4001);

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة",
                definition = longDefinition,
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenEmailIsInvalid()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة جديدة",
                definition = "تعريف جديد",
                email = "not-a-valid-email",
                captchaToken = "valid-token"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitSuggestion_ShouldReturnBadRequest_WhenCaptchaTokenIsMissing()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        using var response = await _client!.PostAsync(
            "/api/words/suggestions",
            JsonContent.Create(new
            {
                headword = "كلمة جديدة",
                definition = "تعريف جديد"
            }),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task ApplyMigrationsAsync(string connectionString)
    {
        var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new AppDbContext(dbContextOptions);
        await db.Database.MigrateAsync();
    }
}
