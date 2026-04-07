using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SudanDialect.Api.Configuration;

namespace SudanDialect.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAdminUsersAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();

        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("AdminUserSeeder");
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AdminSeedOptions>>().Value;

        if (options.Users.Count == 0)
        {
            logger.LogWarning("No admin seed users configured. Set AdminSeed:Users in configuration or user-secrets.");
            return;
        }

        foreach (var configuredUser in options.Users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var username = configuredUser.Username.Trim();
            var password = configuredUser.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Skipped admin seed entry because username or password is empty.");
                continue;
            }

            var existingUser = await userManager.FindByNameAsync(username);
            if (existingUser is not null)
            {
                logger.LogInformation("Admin user '{Username}' already exists.", username);
                continue;
            }

            var user = new IdentityUser
            {
                UserName = username
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                logger.LogInformation("Seeded admin user '{Username}'.", username);
                continue;
            }

            var errors = string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Description}"));
            logger.LogError("Failed to seed admin user '{Username}'. Errors: {Errors}", username, errors);
        }
    }
}
