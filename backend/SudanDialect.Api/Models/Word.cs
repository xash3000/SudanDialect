namespace SudanDialect.Api.Models;

public sealed class Word
{
    public int Id { get; set; }
    public string Headword { get; set; } = string.Empty;
    public string NormalizedHeadword { get; set; } = string.Empty;
    public string Definition { get; set; } = string.Empty;
    public string NormalizedDefinition { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
