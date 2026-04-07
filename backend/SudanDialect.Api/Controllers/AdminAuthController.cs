using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Utilities;

namespace SudanDialect.Api.Controllers;

[ApiController]
[Route("api/admin/auth")]
public sealed class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;

    public AdminAuthController(
        IAdminAuthService adminAuthService)
    {
        _adminAuthService = adminAuthService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(AdminLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdminLoginResponseDto>> Login(
        [FromBody] AdminLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var authResult = await _adminAuthService.LoginAsync(request.Username, request.Password, cancellationToken);
            if (authResult is null)
            {
                return Unauthorized(new { error = "اسم المستخدم أو كلمة المرور غير صحيحة." });
            }

            AppendAuthCookies(
                authResult.Value.AccessToken,
                authResult.Value.AccessExpiresAtUtc,
                authResult.Value.RefreshToken,
                authResult.Value.RefreshExpiresAtUtc);

            return Ok(new AdminLoginResponseDto
            {
                ExpiresAtUtc = authResult.Value.AccessExpiresAtUtc,
                Username = authResult.Value.Username
            });
        }
        catch (InvalidOperationException)
        {
            return Problem(
                title: "JWT configuration is missing",
                detail: "Set Jwt:SigningKey in configuration or user-secrets.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [Authorize]
    [HttpGet("session")]
    [ProducesResponseType(typeof(AdminLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<AdminLoginResponseDto> Session()
    {
        return Ok(_adminAuthService.BuildSessionResponse(User));
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AdminLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<AdminLoginResponseDto>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(AdminAuthCookieNames.RefreshToken, out var refreshTokenValue)
            || string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return Unauthorized();
        }

        try
        {
            var authResult = await _adminAuthService.RefreshAsync(refreshTokenValue, cancellationToken);
            if (authResult is null)
            {
                AppendSignOutCookies();
                return Unauthorized();
            }

            AppendAuthCookies(
                authResult.Value.AccessToken,
                authResult.Value.AccessExpiresAtUtc,
                authResult.Value.RefreshToken,
                authResult.Value.RefreshExpiresAtUtc);

            return Ok(new AdminLoginResponseDto
            {
                ExpiresAtUtc = authResult.Value.AccessExpiresAtUtc,
                Username = authResult.Value.Username
            });
        }
        catch (InvalidOperationException)
        {
            return Problem(
                title: "JWT configuration is missing",
                detail: "Set Jwt:SigningKey in configuration or user-secrets.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Request.Cookies.TryGetValue(AdminAuthCookieNames.RefreshToken, out var refreshTokenValue);
        await _adminAuthService.LogoutAsync(User, refreshTokenValue, cancellationToken);

        AppendSignOutCookies();
        return NoContent();
    }

    private void AppendAuthCookies(string accessToken, DateTime accessExpiresAtUtc, string refreshToken, DateTime refreshExpiresAtUtc)
    {
        Response.Cookies.Append(
            AdminAuthCookieNames.AccessToken,
            accessToken,
            BuildCookieOptions(accessExpiresAtUtc));

        Response.Cookies.Append(
            AdminAuthCookieNames.RefreshToken,
            refreshToken,
            BuildCookieOptions(refreshExpiresAtUtc));
    }

    private void AppendSignOutCookies()
    {
        Response.Cookies.Append(
            AdminAuthCookieNames.AccessToken,
            string.Empty,
            BuildSignOutCookieOptions());

        Response.Cookies.Append(
            AdminAuthCookieNames.RefreshToken,
            string.Empty,
            BuildSignOutCookieOptions());
    }


    private static CookieOptions BuildCookieOptions(DateTime expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = expiresAtUtc,
            Path = "/"
        };
    }

    private static CookieOptions BuildSignOutCookieOptions()
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UnixEpoch,
            Path = "/"
        };
    }

}
