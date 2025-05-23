using System.Collections.Concurrent;
using System.Text;
using Sorter.Comparers;
using Sorter.IO;
using Sorter.Models;

namespace Sorter.Engines.ExternalMerge
{
    /// <summary>
    /// Implements a 2-level k-way merge for sorted run files.
    /// </summary>
    internal sealed class Merger : IMerger
    {
        /// <inheritdoc />
        public async Task MergeAsync(
            IReadOnlyList<string> runs,
            string output,
            int degree,
            CancellationToken ct)
        {
            if (runs.Count <= degree)
            {
                // If all runs fit in a single group, merge them directly
                await MergeGroupAsync(runs, output, ct);
                return;
            }

            var intermediatePaths = new ConcurrentBag<string>();
            var groups = runs.Chunk(degree); // group into batches of N files

            // First-level parallel merge (fan-in)
            await Parallel.ForEachAsync(
                groups,
                new ParallelOptions { MaxDegreeOfParallelism = degree },
                async (group, token) =>
                {
                    string tempPath = Path.Combine(
                        Path.GetDirectoryName(output)!,
                        $"lvl1_{Guid.NewGuid():N}.tmp");

                    await MergeGroupAsync(group, tempPath, token);
                    intermediatePaths.Add(tempPath);
                });

            // Final merge into the target file
            await MergeGroupAsync(intermediatePaths.ToArray(), output, ct);

            // Cleanup temporary files
            foreach (var path in intermediatePaths)
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Merges a group of sorted run files into one output file.
        /// Uses a priority queue for k-way merge.
        /// </summary>
        private async Task MergeGroupAsync(
            IReadOnlyList<string> runs,
            string outPath,
            CancellationToken ct)
        {
            var readers = runs.Select(path => new FastLineReader(path)).ToArray();
            var pq = new PriorityQueue<HeapNode, Key>(runs.Count, KeyComparer.Instance);

            for (int i = 0; i < readers.Length; i++)
            {
                if (readers[i].TryReadLine(out var firstLine, out var key))
                {
                    pq.Enqueue(new HeapNode(i, firstLine!), key);
                }
            }

            await using var outFs = new FileStream(
                outPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1 << 20,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            await using var writer = new StreamWriter(outFs, Encoding.UTF8, bufferSize: 1 << 20);

            while (pq.TryDequeue(out var node, out _))
            {
                await writer.WriteLineAsync(node.Line);

                if (readers[node.ReaderIndex].TryReadLine(out var nextLine, out var nextKey))
                {
                    pq.Enqueue(node with { Line = nextLine! }, nextKey);
                }
            }

            foreach (var reader in readers)
            {
                reader.Dispose();
            }
        }
    }
}
