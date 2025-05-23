namespace Sorter.Options
{
    public sealed class SorterOptions
    {
        public string InputPath { get; init; } = string.Empty;
        public string OutputPath { get; init; } = "sorted.txt";
        public string TempDirectory { get; init; } = "runs";
        public int ChunkSizeMb { get; init; } = 512;
        public int Degree { get; init; } = 8;
    }
}
