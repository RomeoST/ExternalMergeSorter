using Sorter.Buffers;
using Sorter.Models;
using Sorter.Options;
using System.Threading.Channels;

namespace Sorter.Engines.ExternalMerge
{
    public interface IChunkProducer
    {
        Task ProduceAsync(
            SorterOptions sortOptions,
            FixedBufferPool pool,
            ChannelWriter<Chunk> writer,
            CancellationToken ct);
    }
}
