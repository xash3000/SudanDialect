using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos;
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
    public async Task WordsEndpoints_ShouldWorkAgainstRealPostgres()
    {
        _factory.Should().NotBeNull();
        _client.Should().NotBeNull();

        // arrange
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

        // act + assert: search
        using var searchResponse = await _client!.GetAsync(
            $"/api/words/search?query={Uri.EscapeDataString(headword)}",
            TestContext.Current.CancellationToken);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var searchResults = await searchResponse.Content.ReadFromJsonAsync<List<WordSearchResultDto>>(
            cancellationToken: TestContext.Current.CancellationToken);

        searchResults.Should().NotBeNull();
        searchResults!.Should().Contain(result =>
            result.Id == publicId && result.Headword == headword && result.SimilarityScore > 0);

        // act + assert: get by id
        var details = await _client.GetFromJsonAsync<WordDetailsDto>(
            $"/api/words/{publicId}",
            cancellationToken: TestContext.Current.CancellationToken);

        details.Should().NotBeNull();
        details!.Id.Should().Be(publicId);
        details.Headword.Should().Be(headword);
        details.Definition.Should().Be(definition);

        // act + assert: browse
        var letter = headword[..1];
        var browse = await _client.GetFromJsonAsync<WordBrowsePageDto>(
            $"/api/words/browse?letter={Uri.EscapeDataString(letter)}&page=1&pageSize=40",
            cancellationToken: TestContext.Current.CancellationToken);

        browse.Should().NotBeNull();
        browse!.Items.Should().Contain(item => item.Id == publicId && item.Headword == headword);
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
