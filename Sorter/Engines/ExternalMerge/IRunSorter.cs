using Sorter.Buffers;
using Sorter.Models;
using Sorter.Options;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Sorter.Engines.ExternalMerge
{
    public interface IRunSorter
    {
        Task SortAsync(
            ChannelReader<Chunk> reader,
            FixedBufferPool pool,
            SorterOptions sorterOptions,
            ConcurrentBag<string> runPaths,
            CancellationToken ct);
    }
}
