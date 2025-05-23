using System.Threading.Channels;

namespace Generator.Abstractions
{
    public interface IProducerManager
    {
        Task[] RunProducers(Channel<BufferSegment>[] channels, CancellationToken ct);
        Task WaitProducersAsync(Task[] producers, Channel<BufferSegment>[] channels);
    }
}
