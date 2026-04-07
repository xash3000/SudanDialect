namespace SudanDialect.Api.Configuration;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public List<AdminSeedUserOptions> Users { get; set; } = [];
}

public sealed class AdminSeedUserOptions
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
