using Microsoft.Extensions.Logging;
using Polly;
using Polly.Registry;
using Sorter.Abstractions;
using Sorter.Buffers;
using Sorter.Engines.ExternalMerge;
using Sorter.Models;
using Sorter.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Sorter.Engines
{
    /// <summary>
    /// External merge sort engine.
    /// Splits large input file into sorted chunks ("runs"),
    /// then merges them into a final output file using a 2-phase merge.
    /// </summary>
    public sealed class ExternalMergeSortingEngine : ISortingEngine
    {
        private readonly ILogger<ExternalMergeSortingEngine> _log;
        private readonly IChunkProducer _producer;
        private readonly IRunSorter _sorter;
        private readonly IMerger _merger;
        private readonly IPolicyRegistry<string> _policies;

        public ExternalMergeSortingEngine(
            ILogger<ExternalMergeSortingEngine> log,
            IChunkProducer producer,
            IRunSorter sorter,
            IMerger merger,
            IPolicyRegistry<string> policies)
        {
            _log = log;
            _producer = producer;
            _sorter = sorter;
            _merger = merger;
            _policies = policies;
        }

        public async Task SortAsync(SorterOptions sorterOptions, CancellationToken ct)
        {
            ValidateAndPrepareInput(sorterOptions);

            int chunkChars = sorterOptions.ChunkSizeMb * 1024 * 1024 / sizeof(char);
            var pool = new FixedBufferPool(chunkChars, sorterOptions.Degree);

            var runPaths = new ConcurrentBag<string>();
            var channel = Channel.CreateBounded<Chunk>(new BoundedChannelOptions(sorterOptions.Degree)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });

            var retryPolicy = _policies.Get<IAsyncPolicy>("TransientRetry");
            
            // Produce chanks
            var producerTask = retryPolicy.ExecuteAsync(ct => _producer.ProduceAsync(sorterOptions, pool, channel.Writer, ct), ct);

            // Sort chunks in parallel
            var sorterTasks = Enumerable.Range(0, sorterOptions.Degree)
                .Select(_ => retryPolicy.ExecuteAsync(ct => _sorter.SortAsync(channel.Reader, pool, sorterOptions, runPaths, ct), ct))
                .ToArray();

            // Wait for all workers and producer to complete
            await Task.WhenAll(sorterTasks.Append(producerTask));

            _log.LogInformation("{Count} runs generated. Start merging …", runPaths.Count);

            var timeoutPolicy = _policies.Get<IAsyncPolicy>("MergeTimeout");
            
            await timeoutPolicy.ExecuteAsync(ct => 
                _merger.MergeAsync(runPaths.OrderBy(p => p).ToArray(), sorterOptions.OutputPath, sorterOptions.Degree, ct), ct);

            // Cleanup temporary files
            foreach (var p in runPaths) 
                File.Delete(p);
        }

        private static void ValidateAndPrepareInput(SorterOptions options)
        {
            if (!File.Exists(options.InputPath))
                throw new FileNotFoundException(options.InputPath);

            Directory.CreateDirectory(options.TempDirectory);

            foreach (var file in Directory.EnumerateFiles(options.TempDirectory, "run_*.tmp"))
                File.Delete(file);
        }

    }
}
