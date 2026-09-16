using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SudanDialect.Api.Data;
using SudanDialect.Api.Dtos.Admin;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Services;

public sealed class EmbeddingBackfillService : IEmbeddingBackfillService
{
    private const int DefaultBatchSize = 50;
    private const int MaxBatchSize = 500;

    private readonly AppDbContext _dbContext;
    private readonly ITextEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingBackfillService> _logger;

    public EmbeddingBackfillService(
        AppDbContext dbContext,
        ITextEmbeddingService embeddingService,
        ILogger<EmbeddingBackfillService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<EmbeddingBackfillStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var totalWords = await _dbContext.Words.CountAsync(cancellationToken);
        var wordsWithEmbeddings = await _dbContext.Words.CountAsync(w => w.Embedding != null, cancellationToken);

        return new EmbeddingBackfillStatusDto
        {
            IsEmbeddingServiceAvailable = _embeddingService.IsAvailable,
            Dimensions = _embeddingService.Dimensions,
            TotalWords = totalWords,
            WordsWithEmbeddings = wordsWithEmbeddings,
            WordsWithoutEmbeddings = Math.Max(0, totalWords - wordsWithEmbeddings)
        };
    }

    public async Task<EmbeddingBackfillResultDto> BackfillEmbeddingsAsync(
        int batchSize = DefaultBatchSize,
        bool forceAll = false,
        CancellationToken cancellationToken = default)
    {
        if (!_embeddingService.IsAvailable)
        {
            _logger.LogWarning("Embedding service is not available. Skipping backfill.");
            var initialStatus = await GetStatusAsync(cancellationToken);
            return new EmbeddingBackfillResultDto
            {
                BackfilledCount = 0,
                RemainingMissing = initialStatus.WordsWithoutEmbeddings,
                TotalWords = initialStatus.TotalWords,
                WordsWithEmbeddings = initialStatus.WordsWithEmbeddings
            };
        }

        if (batchSize <= 0)
        {
            batchSize = DefaultBatchSize;
        }
        else if (batchSize > MaxBatchSize)
        {
            batchSize = MaxBatchSize;
        }

        var totalProcessed = 0;
        var lastId = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var wordsQuery = forceAll
                ? _dbContext.Words.Where(w => w.Id > lastId).OrderBy(w => w.Id)
                : _dbContext.Words.Where(w => w.Embedding == null && w.Id > lastId).OrderBy(w => w.Id);

            var batch = await wordsQuery
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var word in batch)
            {
                var textToEmbed = $"{word.NormalizedHeadword} {word.NormalizedDefinition}";
                cancellationToken.ThrowIfCancellationRequested();
                var embedding = _embeddingService.GenerateEmbedding(textToEmbed);
                word.Embedding = embedding;
                lastId = Math.Max(lastId, word.Id);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            totalProcessed += batch.Count;

            _logger.LogInformation(
                "Backfilled embeddings for {Count} words (total so far: {TotalProcessed}, forceAll: {ForceAll}).",
                batch.Count,
                totalProcessed,
                forceAll);
        }

        if (totalProcessed > 0)
        {
            await ReindexAsync(cancellationToken);
        }

        var status = await GetStatusAsync(cancellationToken);

        return new EmbeddingBackfillResultDto
        {
            BackfilledCount = totalProcessed,
            RemainingMissing = status.WordsWithoutEmbeddings,
            TotalWords = status.TotalWords,
            WordsWithEmbeddings = status.WordsWithEmbeddings
        };
    }

    public async Task<int> BackfillMissingEmbeddingsAsync(int batchSize = DefaultBatchSize, CancellationToken cancellationToken = default)
    {
        var result = await BackfillEmbeddingsAsync(batchSize, forceAll: false, cancellationToken);
        return result.BackfilledCount;
    }

    public async Task ReindexAsync(CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            _logger.LogInformation("Reindexing IX_words_Embedding index...");
            await _dbContext.Database.ExecuteSqlRawAsync("REINDEX INDEX \"IX_words_Embedding\";", cancellationToken);
            _logger.LogInformation("Successfully reindexed IX_words_Embedding.");
        }
        else
        {
            _logger.LogInformation("Database is not Npgsql; skipping index reindexing.");
        }
    }
}
