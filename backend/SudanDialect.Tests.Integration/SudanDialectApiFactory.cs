using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Tests.Integration;

public sealed class SudanDialectApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _jwtSigningKey;

    public SudanDialectApiFactory(string connectionString, string jwtSigningKey)
    {
        _connectionString = connectionString;
        _jwtSigningKey = jwtSigningKey;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Jwt:SigningKey"] = _jwtSigningKey,
                ["PublicId:MinLength"] = "8",
                ["Turnstile:SecretKey"] = "test-secret-key"
            };

            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ITurnstileVerificationService>(new MockTurnstileVerificationService());
        });
    }

    private sealed class MockTurnstileVerificationService : ITurnstileVerificationService
    {
        public Task<bool> VerifyAsync(string token, string? remoteIp, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(!string.IsNullOrWhiteSpace(token));
        }
    }
}
