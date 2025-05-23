using System.Threading.Channels;

namespace Generator.Abstractions
{
    public interface IProducer
    {
        Task ProduceAsync(ChannelWriter<BufferSegment> writer, long targetBytes, CancellationToken ct);
    }
}
