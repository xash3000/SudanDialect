using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SudanDialect.Api.Services;

public sealed class AdminAuthService : IAdminAuthService
{
    private const string RefreshTokenLoginProvider = "SudanDialect.Admin";
    private const string RefreshTokenName = "RefreshToken";

    private readonly UserManager<IdentityUser> _userManager;
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly JwtOptions _jwtOptions;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public AdminAuthService(
        UserManager<IdentityUser> userManager,
        SignInManager<IdentityUser> signInManager,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc, string Username)?> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedUsername = username.Trim();
        var user = await _userManager.FindByNameAsync(normalizedUsername);

        if (user is null)
        {
            return null;
        }

        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: false);
        if (!passwordCheck.Succeeded)
        {
            return null;
        }

        EnsureSigningKeyConfigured();

        var accessTokenTtlMinutes = Math.Clamp(_jwtOptions.ExpirationMinutes, 10, 15);
        var accessExpiresAtUtc = DateTime.UtcNow.AddMinutes(accessTokenTtlMinutes);
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenExpirationDays));

        var accessToken = GenerateAccessToken(user, normalizedUsername, accessExpiresAtUtc);
        var refreshToken = CreateRefreshTokenValue(user.Id);

        await PersistRefreshTokenAsync(user, refreshToken, refreshExpiresAtUtc);

        return (
            accessToken,
            accessExpiresAtUtc,
            refreshToken,
            refreshExpiresAtUtc,
            user.UserName ?? normalizedUsername);
    }

    public AdminLoginResponseDto BuildSessionResponse(ClaimsPrincipal user)
    {
        var username = user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
            ?? string.Empty;

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(Math.Clamp(_jwtOptions.ExpirationMinutes, 10, 15));
        var expClaim = user.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (long.TryParse(expClaim, out var expUnixSeconds))
        {
            expiresAtUtc = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds).UtcDateTime;
        }

        return new AdminLoginResponseDto
        {
            ExpiresAtUtc = expiresAtUtc,
            Username = username
        };
    }

    public async Task<(string AccessToken, DateTime AccessExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc, string Username)?> RefreshAsync(
        string refreshTokenValue,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        EnsureSigningKeyConfigured();

        if (string.IsNullOrWhiteSpace(refreshTokenValue)
            || !TryGetUserIdFromRefreshToken(refreshTokenValue, out var userId))
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        var isValidRefreshToken = await ValidateRefreshTokenAsync(user, refreshTokenValue);
        if (!isValidRefreshToken)
        {
            await RemoveRefreshTokenAsync(user);
            return null;
        }

        var accessTokenTtlMinutes = Math.Clamp(_jwtOptions.ExpirationMinutes, 10, 15);
        var accessExpiresAtUtc = DateTime.UtcNow.AddMinutes(accessTokenTtlMinutes);
        var refreshExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Max(1, _jwtOptions.RefreshTokenExpirationDays));

        var accessToken = GenerateAccessToken(user, user.UserName ?? userId, accessExpiresAtUtc);
        var refreshedToken = CreateRefreshTokenValue(user.Id);

        await PersistRefreshTokenAsync(user, refreshedToken, refreshExpiresAtUtc);

        return (
            accessToken,
            accessExpiresAtUtc,
            refreshedToken,
            refreshExpiresAtUtc,
            user.UserName ?? userId);
    }

    public async Task LogoutAsync(ClaimsPrincipal user, string? refreshTokenValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var identityUser = await _userManager.FindByIdAsync(userId);
                if (identityUser is not null)
                {
                    await RemoveRefreshTokenAsync(identityUser);
                }
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(refreshTokenValue)
            || !TryGetUserIdFromRefreshToken(refreshTokenValue, out var refreshUserId))
        {
            return;
        }

        var refreshUser = await _userManager.FindByIdAsync(refreshUserId);
        if (refreshUser is not null)
        {
            await RemoveRefreshTokenAsync(refreshUser);
        }
    }

    private string GenerateAccessToken(IdentityUser user, string fallbackUsername, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? fallbackUsername),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? fallbackUsername),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAtUtc,
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    private async Task PersistRefreshTokenAsync(IdentityUser user, string refreshTokenValue, DateTime expiresAtUtc)
    {
        var payload = new StoredRefreshToken
        {
            TokenHash = ComputeSha256(refreshTokenValue),
            ExpiresAtUtc = expiresAtUtc
        };

        var serializedPayload = JsonSerializer.Serialize(payload);
        await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenLoginProvider, RefreshTokenName, serializedPayload);
    }

    private async Task<bool> ValidateRefreshTokenAsync(IdentityUser user, string refreshTokenValue)
    {
        var rawToken = await _userManager.GetAuthenticationTokenAsync(user, RefreshTokenLoginProvider, RefreshTokenName);
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return false;
        }

        StoredRefreshToken? payload;

        try
        {
            payload = JsonSerializer.Deserialize<StoredRefreshToken>(rawToken);
        }
        catch
        {
            return false;
        }

        if (payload is null || string.IsNullOrWhiteSpace(payload.TokenHash) || payload.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return false;
        }

        var expectedHash = ComputeSha256(refreshTokenValue);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var actualBytes = Encoding.UTF8.GetBytes(payload.TokenHash);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private async Task RemoveRefreshTokenAsync(IdentityUser user)
    {
        await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenLoginProvider, RefreshTokenName);
    }

    private static bool TryGetUserIdFromRefreshToken(string refreshTokenValue, out string userId)
    {
        userId = string.Empty;

        var separatorIndex = refreshTokenValue.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex >= refreshTokenValue.Length - 1)
        {
            return false;
        }

        userId = refreshTokenValue[..separatorIndex];
        return !string.IsNullOrWhiteSpace(userId);
    }

    private static string CreateRefreshTokenValue(string userId)
    {
        var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48));
        return $"{userId}:{nonce}";
    }

    private static string ComputeSha256(string input)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes);
    }

    private void EnsureSigningKeyConfigured()
    {
        if (string.IsNullOrWhiteSpace(_jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("JWT configuration is missing");
        }
    }

    private class StoredRefreshToken
    {
        public string TokenHash { get; init; } = string.Empty;
        public DateTime ExpiresAtUtc { get; init; }
    }
}
