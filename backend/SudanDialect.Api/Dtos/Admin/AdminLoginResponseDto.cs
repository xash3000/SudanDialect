namespace SudanDialect.Api.Dtos.Admin;

public sealed class AdminLoginResponseDto
{
    public DateTime ExpiresAtUtc { get; init; }
    public string Username { get; init; } = string.Empty;
}
