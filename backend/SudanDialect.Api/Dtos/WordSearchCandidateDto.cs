namespace SudanDialect.Api.Dtos;

public sealed class WordSearchCandidateDto
{
    public int Id { get; init; }
    public string Headword { get; init; } = string.Empty;
    public string Definition { get; init; } = string.Empty;
    public double SimilarityScore { get; init; }
}
