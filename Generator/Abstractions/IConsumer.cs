using System.Threading.Channels;

namespace Generator.Abstractions
{
    public interface IConsumer
    {
        Task ConsumeAsync(ChannelReader<BufferSegment> reader, CancellationToken ct);
    }
}
