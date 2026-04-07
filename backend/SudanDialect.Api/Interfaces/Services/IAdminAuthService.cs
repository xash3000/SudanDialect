using SudanDialect.Api.Dtos.Admin;
using System.Security.Claims;

namespace SudanDialect.Api.Interfaces.Services;

public interface IAdminAuthService
{
    Task<(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc, string Username)?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);

    AdminLoginResponseDto BuildSessionResponse(ClaimsPrincipal user);

    Task<(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc, string Username)?> RefreshAsync(
        string refreshTokenValue,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(ClaimsPrincipal user, string? refreshTokenValue, CancellationToken cancellationToken = default);
}
