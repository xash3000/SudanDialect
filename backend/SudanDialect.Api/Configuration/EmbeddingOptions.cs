namespace SudanDialect.Api.Configuration;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public bool Enabled { get; set; } = true;
    public bool AutoBackfillOnStartup { get; set; } = true;
    public string ModelPath { get; set; } = "onnx_models/silma-embedding-matryoshka-v0.1.onnx";
    public string VocabPath { get; set; } = "onnx_models/vocab.txt";
    public string TokenizerJsonPath { get; set; } = "onnx_models/tokenizer.json";
    public int Dimensions { get; set; } = 768;
    public int MaxSequenceLength { get; set; } = 128;
    public int IntraOpNumThreads { get; set; } = 1;
    public int InterOpNumThreads { get; set; } = 1;
}
