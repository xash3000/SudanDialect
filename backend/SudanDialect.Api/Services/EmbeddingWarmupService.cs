namespace SudanDialect.Api.Services;

public sealed class EmbeddingWarmupService : IHostedService
{
    private readonly TextEmbeddingService _embeddingService;
    private readonly ILogger<EmbeddingWarmupService> _logger;

    public EmbeddingWarmupService(
        TextEmbeddingService embeddingService,
        ILogger<EmbeddingWarmupService> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _embeddingService.EnsureInitialized();
        _logger.LogInformation(
            "Embedding warmup complete. Available: {IsAvailable}.",
            _embeddingService.IsAvailable);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
