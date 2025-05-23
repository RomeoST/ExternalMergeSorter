using Sorter.Buffers;
using Sorter.Models;
using Sorter.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Sorter.Comparers;

namespace Sorter.Engines.ExternalMerge
{
    /// <summary>
    /// Sorts incoming chunks and writes sorted runs to temporary files.
    /// Each chunk is processed independently and produces one sorted run file.
    /// </summary>
    internal class RunSorter : IRunSorter
    {
        public async Task SortAsync(
            ChannelReader<Chunk> reader,
            FixedBufferPool pool,
            SorterOptions sorterOptions,
            ConcurrentBag<string> runPaths,
            CancellationToken ct)
        {
            await foreach (var chunk in reader.ReadAllAsync(ct))
            {
                // Sort the entries within the chunk using buffer-based comparison
                Array.Sort(chunk.Entries, new BufferEntryComparer(chunk.Buffer));

                // Generate unique file name for the sorted run
                string path = Path.Combine(sorterOptions.TempDirectory, $"run_{Guid.NewGuid():N}.tmp");
                
                await using var fs = new FileStream(
                    path, 
                    FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    1 << 20, 
                    FileOptions.Asynchronous);
                using var writer = new StreamWriter(fs, Encoding.UTF8, 1 << 20);

                foreach (var e in chunk.Entries)
                {
                    writer.Write(e.Number);
                    writer.Write(". ");
                    writer.Write(chunk.Buffer.AsSpan(e.Start, e.Length));
                    writer.Write('\n');
                }

                await writer.FlushAsync(ct);

                // Track the path of the written run for future merging
                runPaths.Add(path);

                pool.Return(chunk.Buffer);
            }
        }
    }
}
