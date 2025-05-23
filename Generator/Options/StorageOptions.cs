using System.ComponentModel.DataAnnotations;

namespace Generator.Options
{
    public sealed record StorageOptions
    {
        [Required] public string OutputPath { get; init; } = "data/big.txt";
        public string OutputPathPattern { get; init; } = "data/big-part-{0}.tmp";
        public int PartitionCount { get; init; } = 8;

        [Required] public string TargetSize { get; init; } = "1GB";
        [Range(1, int.MaxValue)] public int BufferSize { get; init; } = 1 << 20;
        [Required] public string WordListPath { get; init; } = "words.json";
        [Range(1, int.MaxValue)] public int MaxNumber { get; init; } = 100_000;
    }
}
