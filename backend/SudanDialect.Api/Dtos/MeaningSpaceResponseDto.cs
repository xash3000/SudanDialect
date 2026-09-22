namespace SudanDialect.Api.Dtos;

public sealed class MeaningSpaceResponseDto
{
    public IReadOnlyList<WordVectorNeighborDto> Words { get; init; } = [];
}
