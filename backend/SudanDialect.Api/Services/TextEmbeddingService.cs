using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Pgvector;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Interfaces.Services;

namespace SudanDialect.Api.Services;

public sealed class TextEmbeddingService : ITextEmbeddingService, IDisposable
{
    private readonly ILogger<TextEmbeddingService> _logger;
    private readonly EmbeddingOptions _options;
    private readonly object _syncRoot = new();

    private InferenceSession? _session;
    private BertTokenizer? _tokenizer;
    private volatile bool _isAvailable;
    private bool _initialized;
    private bool _disposed;
    private int _clsId = 2;
    private int _sepId = 3;

    public TextEmbeddingService(
        IOptions<EmbeddingOptions> options,
        ILogger<TextEmbeddingService> logger)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool IsAvailable => _isAvailable && !_disposed;
    public int Dimensions => _options.Dimensions;

    public void EnsureInitialized()
    {
        if (_initialized || _disposed)
        {
            return;
        }

        lock (_syncRoot)
        {
            if (_initialized || _disposed)
            {
                return;
            }

            _initialized = true;
            Initialize();
        }
    }

    private void Initialize()
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Text embedding service is disabled via configuration.");
            return;
        }

        var modelPath = ResolvePath(_options.ModelPath);
        var vocabPath = ResolvePath(_options.VocabPath);

        if (!File.Exists(modelPath) || !File.Exists(vocabPath))
        {
            _logger.LogWarning(
                "Embedding model or vocab not found (model: '{ModelPath}', vocab: '{VocabPath}'). Semantic search disabled.",
                modelPath,
                vocabPath);
            return;
        }

        try
        {
            using var vocabStream = File.OpenRead(vocabPath);
            _tokenizer = BertTokenizer.Create(vocabStream);
            ResolveSpecialIds(vocabPath);

            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                IntraOpNumThreads = Math.Max(1, _options.IntraOpNumThreads),
                InterOpNumThreads = Math.Max(1, _options.InterOpNumThreads),
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
            };

            _session = new InferenceSession(modelPath, sessionOptions);
            _isAvailable = true;

            _logger.LogInformation(
                "Text embedding service initialized successfully. Dimensions: {Dimensions}, MaxSequenceLength: {MaxSequenceLength}.",
                _options.Dimensions,
                _options.MaxSequenceLength);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ONNX embedding session from '{ModelPath}'.", modelPath);
            DisposeResources();
        }
    }

    private void ResolveSpecialIds(string vocabPath)
    {
        try
        {
            var lines = File.ReadAllLines(vocabPath);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i] == "[CLS]")
                {
                    _clsId = i;
                }
                else if (lines[i] == "[SEP]")
                {
                    _sepId = i;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve [CLS]/[SEP] ids from vocab; using defaults CLS={ClsId} SEP={SepId}.", _clsId, _sepId);
        }
    }

    public Vector? GenerateEmbedding(string text)
    {
        EnsureInitialized();

        if (string.IsNullOrWhiteSpace(text) || !_isAvailable || _disposed)
        {
            return null;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var trimmed = text.Trim();
            var charCap = Math.Max(512, _options.MaxSequenceLength * 32);
            if (trimmed.Length > charCap)
            {
                trimmed = trimmed.Substring(0, charCap);
            }

            InferenceSession session;
            BertTokenizer tokenizer;
            int clsId;
            int sepId;
            int maxLen;

            lock (_syncRoot)
            {
                if (_session == null || _tokenizer == null || _disposed)
                {
                    return null;
                }

                session = _session;
                tokenizer = _tokenizer;
                clsId = _clsId;
                sepId = _sepId;
                maxLen = Math.Max(3, _options.MaxSequenceLength);
            }

            var tokenIds = tokenizer.EncodeToIds(trimmed);
            if (tokenIds == null || tokenIds.Count == 0)
            {
                return null;
            }

            var finalIds = TruncateToMaxLength(tokenIds, maxLen, clsId, sepId);
            var count = finalIds.Count;
            if (count <= 2)
            {
                return null;
            }

            var inputIds = new DenseTensor<long>(new[] { 1, count });
            var attentionMask = new DenseTensor<long>(new[] { 1, count });
            var tokenTypeIds = new DenseTensor<long>(new[] { 1, count });
            var poolMask = new long[count];

            for (var i = 0; i < count; i++)
            {
                inputIds[0, i] = finalIds[i];
                attentionMask[0, i] = 1;
                tokenTypeIds[0, i] = 0;
                poolMask[i] = (i == 0 || i == count - 1) ? 0 : 1;
            }

            List<NamedOnnxValue> inputs;
            lock (_syncRoot)
            {
                if (_session == null || _disposed)
                {
                    return null;
                }

                var inputNames = session.InputMetadata.Keys.ToHashSet(StringComparer.Ordinal);
                inputs = new List<NamedOnnxValue>(3);
                if (!inputNames.Contains("input_ids"))
                {
                    _logger.LogError("ONNX model is missing required 'input_ids' input.");
                    return null;
                }

                inputs.Add(NamedOnnxValue.CreateFromTensor("input_ids", inputIds));
                if (inputNames.Contains("attention_mask"))
                {
                    inputs.Add(NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask));
                }

                if (inputNames.Contains("token_type_ids"))
                {
                    inputs.Add(NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds));
                }
            }

            Tensor<float> output;
            lock (_syncRoot)
            {
                if (_session == null || _disposed)
                {
                    return null;
                }

                using var results = session.Run(inputs);
                var tensor = results.FirstOrDefault()?.AsTensor<float>();
                if (tensor == null)
                {
                    return null;
                }

                output = tensor;
            }

            var hiddenDim = output.Dimensions[^1];
            var pooled = MeanPool(output, poolMask, count, hiddenDim);
            if (pooled.All(v => v == 0f))
            {
                return null;
            }

            float[] finalVector;
            if (hiddenDim == _options.Dimensions)
            {
                NormalizeL2(pooled);
                finalVector = pooled;
            }
            else if (hiddenDim > _options.Dimensions)
            {
                finalVector = TruncateAndNormalize(pooled, _options.Dimensions);
            }
            else
            {
                _logger.LogWarning(
                    "Embedding hidden dim {HiddenDim} is smaller than configured {Dimensions}; skipping.",
                    hiddenDim,
                    _options.Dimensions);
                return null;
            }

            _logger.LogDebug(
                "Generated embedding in {ElapsedMs}ms (seqLen: {SeqLen}, hiddenDim: {HiddenDim}).",
                stopwatch.ElapsedMilliseconds,
                count,
                hiddenDim);

            return new Vector(finalVector);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text.");
            return null;
        }
    }

    private static List<int> TruncateToMaxLength(IReadOnlyList<int> tokenIds, int maxLen, int clsId, int sepId)
    {
        if (tokenIds.Count <= maxLen)
        {
            return new List<int>(tokenIds);
        }

        var result = new List<int>(maxLen) { clsId };
        var start = tokenIds.Count > 0 && tokenIds[0] == clsId ? 1 : 0;
        for (var i = start; i < tokenIds.Count && result.Count < maxLen - 1; i++)
        {
            var id = tokenIds[i];
            if (id == clsId || id == sepId)
            {
                continue;
            }

            result.Add(id);
        }

        result.Add(sepId);
        return result;
    }

    public static float[] MeanPool(Tensor<float> tensor, int seqLen, int hiddenDim)
    {
        var pooled = new float[hiddenDim];
        if (seqLen <= 0)
        {
            return pooled;
        }

        for (var i = 0; i < seqLen; i++)
        {
            for (var d = 0; d < hiddenDim; d++)
            {
                pooled[d] += tensor[0, i, d];
            }
        }

        for (var d = 0; d < hiddenDim; d++)
        {
            pooled[d] /= seqLen;
        }

        return pooled;
    }

    public static float[] MeanPool(Tensor<float> tensor, long[] attentionMask, int seqLen, int hiddenDim)
    {
        var pooled = new float[hiddenDim];
        var maskSum = 0f;

        for (var i = 0; i < seqLen; i++)
        {
            if (attentionMask[i] == 1)
            {
                maskSum += 1f;
                for (var d = 0; d < hiddenDim; d++)
                {
                    pooled[d] += tensor[0, i, d];
                }
            }
        }

        if (maskSum > 0f)
        {
            for (var d = 0; d < hiddenDim; d++)
            {
                pooled[d] /= maskSum;
            }
        }

        return pooled;
    }

    public static void NormalizeL2(Span<float> vector)
    {
        var sumSquares = 0.0;
        for (var i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        var norm = Math.Sqrt(sumSquares);
        if (norm > 1e-12)
        {
            var factor = (float)(1.0 / norm);
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] *= factor;
            }
        }
    }

    public static float[] TruncateAndNormalize(float[] rawEmbedding, int targetDimensions)
    {
        var targetDim = Math.Min(targetDimensions, rawEmbedding.Length);
        var result = new float[targetDim];
        Array.Copy(rawEmbedding, result, targetDim);
        NormalizeL2(result);
        return result;
    }

    private static string ResolvePath(string relativeOrAbsolutePath)
    {
        if (Path.IsPathRooted(relativeOrAbsolutePath))
        {
            return relativeOrAbsolutePath;
        }

        var baseDirCandidate = Path.Combine(AppContext.BaseDirectory, relativeOrAbsolutePath);
        if (File.Exists(baseDirCandidate) || Directory.Exists(baseDirCandidate))
        {
            return baseDirCandidate;
        }

        var currentDirCandidate = Path.Combine(Directory.GetCurrentDirectory(), relativeOrAbsolutePath);
        if (File.Exists(currentDirCandidate) || Directory.Exists(currentDirCandidate))
        {
            return currentDirCandidate;
        }

        return baseDirCandidate;
    }

    private void DisposeResources()
    {
        _session?.Dispose();
        _session = null;
        _tokenizer = null;
        _isAvailable = false;
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeResources();
        }
    }
}
