namespace SudanDialect.Api.Dtos.Admin;

public sealed class EmbeddingBackfillStatusDto
{
    public bool IsEmbeddingServiceAvailable { get; init; }
    public int Dimensions { get; init; }
    public int TotalWords { get; init; }
    public int WordsWithEmbeddings { get; init; }
    public int WordsWithoutEmbeddings { get; init; }
}
