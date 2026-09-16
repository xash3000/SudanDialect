namespace SudanDialect.Api.Dtos.Admin;

public sealed class EmbeddingBackfillResultDto
{
    public int BackfilledCount { get; init; }
    public int RemainingMissing { get; init; }
    public int TotalWords { get; init; }
    public int WordsWithEmbeddings { get; init; }
}
