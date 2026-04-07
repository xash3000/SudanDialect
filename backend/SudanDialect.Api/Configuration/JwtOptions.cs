namespace SudanDialect.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "SudanDialect.Api";
    public string Audience { get; set; } = "SudanDialect.Admin";
    public string SigningKey { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 14;
}
