using Pgvector;

namespace SudanDialect.Api.Interfaces.Services;

public interface ITextEmbeddingService
{
    bool IsAvailable { get; }
    int Dimensions { get; }
    void EnsureInitialized();
    Vector? GenerateEmbedding(string text);
}
