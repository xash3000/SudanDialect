using FluentAssertions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Services;
using Xunit;

namespace SudanDialect.Tests.Unit.Services;

public class PcaProjectionServiceTests
{
    private readonly IPcaProjectionService _sut = new PcaProjectionService();

    [Fact]
    public void ProjectTo3D_ShouldReturnEmptyResult_WhenInputIsEmpty()
    {
        var result = _sut.ProjectTo3D([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ProjectTo3D_ShouldReturnOrigin_WhenInputIsSingleVector()
    {
        var result = _sut.ProjectTo3D([new float[] { 1f, 2f, 3f, 4f }]);

        result.Should().HaveCount(1);
        result[0].Should().Equal(0f, 0f, 0f);
    }

    [Fact]
    public void ProjectTo3D_ShouldReturnThreeCoordinates_ForEachInputVector()
    {
        var input = new[]
        {
            new float[] { 1f, 2f, 3f },
            new float[] { 4f, 5f, 6f },
            new float[] { 7f, 8f, 9f },
            new float[] { 2f, 0f, 1f }
        };

        var result = _sut.ProjectTo3D(input);

        result.Should().HaveCount(input.Length);
        foreach (var coords in result)
        {
            coords.Should().HaveCount(3);
        }
    }

    [Fact]
    public void ProjectTo3D_ShouldProduceCenteredProjections_WhenInputHasAtLeastThreeVectors()
    {
        var input = new[]
        {
            new float[] { 1f, 2f, 3f, 4f },
            new float[] { 4f, 5f, 6f, 7f },
            new float[] { 7f, 8f, 9f, 10f },
            new float[] { 2f, 0f, 1f, 3f },
            new float[] { 9f, 1f, 4f, 2f }
        };

        var result = _sut.ProjectTo3D(input);

        var mean = new double[] { 0, 0, 0 };
        foreach (var coords in result)
        {
            for (var i = 0; i < 3; i++)
            {
                mean[i] += coords[i];
            }
        }

        for (var i = 0; i < 3; i++)
        {
            mean[i] /= result.Length;
            mean[i].Should().BeApproximately(0d, 1e-3);
        }
    }

    [Fact]
    public void ProjectTo3D_ShouldZeroOutUnusedCoordinates_WhenFewerThanThreeVectors()
    {
        var input = new[]
        {
            new float[] { 1f, 0f, 0f },
            new float[] { 0f, 1f, 0f }
        };

        var result = _sut.ProjectTo3D(input);

        result.Should().HaveCount(2);
        result[0][2].Should().Be(0f);
        result[1][2].Should().Be(0f);
    }

    [Fact]
    public void ProjectTo3D_ShouldBeDeterministic_ForSameInput()
    {
        var input = new[]
        {
            new float[] { 1f, 2f, 3f },
            new float[] { 4f, 5f, 6f },
            new float[] { 7f, 8f, 9f }
        };

        var first = _sut.ProjectTo3D(input);
        var second = _sut.ProjectTo3D(input);

        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void ProjectTo3D_ShouldPreservePairwiseDistances_WhenRankIsFullyCaptured()
    {
        // With at most 3 points, the centered data has rank <= 2, so keeping the top 3
        // components is an isometry: every pairwise distance must survive unchanged.
        var points = new[]
        {
            new float[] { 1f, 2f, 3f, 4f, 5f },
            new float[] { 6f, 7f, 8f, 9f, 10f },
            new float[] { 0f, 3f, 9f, 1f, 2f }
        };

        var result = _sut.ProjectTo3D(points);

        for (var i = 0; i < points.Length; i++)
        {
            for (var j = i + 1; j < points.Length; j++)
            {
                Distance(points[i], points[j])
                    .Should()
                    .BeApproximately(Distance(result[i], result[j]), 1e-2);
            }
        }
    }

    [Fact]
    public void ProjectTo3D_ShouldMatchManualSvdProjection()
    {
        var input = new[]
        {
            new float[] { 1f, 2f, 3f, 4f },
            new float[] { 4f, 5f, 6f, 7f },
            new float[] { 7f, 8f, 9f, 10f },
            new float[] { 2f, 0f, 1f, 3f },
            new float[] { 9f, 1f, 4f, 2f }
        };

        var result = _sut.ProjectTo3D(input);
        var reference = ComputeReferenceProjection(input);

        result.Should().HaveCount(reference.Length);
        for (var i = 0; i < result.Length; i++)
        {
            for (var j = 0; j < 3; j++)
            {
                result[i][j].Should().BeApproximately(reference[i][j], 1e-4f);
            }
        }
    }

    private static double Distance(float[] a, float[] b)
    {
        var sum = 0d;
        for (var i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    private static float[][] ComputeReferenceProjection(IReadOnlyList<float[]> vectors)
    {
        var n = vectors.Count;
        var d = vectors[0].Length;

        var data = new float[n * d];
        for (var i = 0; i < n; i++)
        {
            Array.Copy(vectors[i], 0, data, i * d, d);
        }

        var matrix = DenseMatrix.Build.Dense(n, d, (i, j) => data[i * d + j]);

        var mean = new float[d];
        for (var j = 0; j < d; j++)
        {
            var sum = 0f;
            for (var i = 0; i < n; i++)
            {
                sum += matrix[i, j];
            }

            mean[j] = sum / n;
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < d; j++)
            {
                matrix[i, j] -= mean[j];
            }
        }

        var svd = matrix.Svd(computeVectors: true);
        var vt = svd.VT;

        var componentCount = Math.Min(3, Math.Min(n, vt.RowCount));
        var components = vt.SubMatrix(0, componentCount, 0, d).Transpose();
        var projected = matrix.Multiply(components);

        var result = new float[n][];
        for (var i = 0; i < n; i++)
        {
            result[i] = new float[3];
            for (var j = 0; j < componentCount; j++)
            {
                result[i][j] = projected[i, j];
            }
        }

        return result;
    }
}