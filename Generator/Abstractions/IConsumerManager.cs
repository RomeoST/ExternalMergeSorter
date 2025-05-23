using System.Threading.Channels;

namespace Generator.Abstractions
{
    public interface IConsumerManager
    {
        (Channel<BufferSegment>[] channels, Task[] consumers) CreateConsumers(CancellationToken ct);
    }
}
