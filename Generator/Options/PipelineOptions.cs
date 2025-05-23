using System.ComponentModel.DataAnnotations;

namespace Generator.Options
{
    public sealed record PipelineOptions
    {
        [Range(0, 100)] public int DuplicatePercentage { get; init; } = 10;
        public int ChannelCapacity { get; init; } = 4096;
        public int FlushIntervalSec { get; init; } = 5;
        public int RetryCount { get; init; } = 3;
        public int WorkerCount { get; init; } = Math.Max(1, Environment.ProcessorCount / 2);
        public int BatchWrite { get; init; } = 32;
    }
}
