using SudanDialect.Api.Dtos.Admin;

namespace SudanDialect.Api.Interfaces.Services;

public interface IEmbeddingBackfillService
{
    Task<EmbeddingBackfillStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<EmbeddingBackfillResultDto> BackfillEmbeddingsAsync(
        int batchSize = 50,
        bool forceAll = false,
        CancellationToken cancellationToken = default);

    Task<int> BackfillMissingEmbeddingsAsync(
        int batchSize = 50,
        CancellationToken cancellationToken = default);

    Task ReindexAsync(CancellationToken cancellationToken = default);
}
