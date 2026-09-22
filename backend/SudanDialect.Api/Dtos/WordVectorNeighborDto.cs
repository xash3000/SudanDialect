namespace SudanDialect.Api.Dtos;

public sealed class WordVectorNeighborDto
{
    public string Id { get; init; } = string.Empty;
    public string Headword { get; init; } = string.Empty;
    public string Definition { get; init; } = string.Empty;
    public double SimilarityScore { get; init; }
    public bool IsSelected { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
}
