using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Services;

public sealed class PcaProjectionService : IPcaProjectionService
{
    public float[][] ProjectTo3D(IReadOnlyList<float[]> vectors)
    {
        if (vectors.Count == 0)
        {
            return [];
        }

        if (vectors.Count == 1)
        {
            return [new float[] { 0f, 0f, 0f }];
        }

        var n = vectors.Count;
        var d = vectors[0].Length;

        var data = new float[n * d];
        for (var i = 0; i < n; i++)
        {
            Array.Copy(vectors[i], 0, data, i * d, d);
        }

        var matrix = DenseMatrix.Build.Dense(n, d, (i, j) => data[i * d + j]);

        // Center the data
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

        // SVD: matrix = U * S * VT
        var svd = matrix.Svd(computeVectors: true);
        var vt = svd.VT;

        var componentCount = Math.Min(3, Math.Min(n, vt.RowCount));

        // Project: result = centered_matrix * V[:, :3]
        // V[:, :3] = VT[:3, :]^T
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
