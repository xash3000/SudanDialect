namespace SudanDialect.Api.Interfaces.Services;

public interface IPcaProjectionService
{
    float[][] ProjectTo3D(IReadOnlyList<float[]> vectors);
}