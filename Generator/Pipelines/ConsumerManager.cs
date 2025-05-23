using System.Threading.Channels;
using Generator.Abstractions;
using Generator.Infrastructure.IO;
using Generator.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generator.Pipelines
{
    /// <summary>
    /// Creates one bounded channel + BatchConsumer per partition.
    /// </summary>
    internal sealed class ConsumerManager : IConsumerManager
    {
        private readonly StorageOptions _storage;
        private readonly PipelineOptions _pipeline;

        public ConsumerManager(
            IOptions<StorageOptions> storageOptions,
            IOptions<PipelineOptions> pipelineOptions,
            ILoggerFactory loggerFactory)
        {
            _storage = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _pipeline = pipelineOptions?.Value ?? throw new ArgumentNullException(nameof(pipelineOptions));
        }

        public (Channel<BufferSegment>[] channels, Task[] consumers) CreateConsumers(CancellationToken ct)
        {
            int parts = _storage.PartitionCount;
            var channels = new Channel<BufferSegment>[parts];
            var consumers = new Task[parts];

            var channelOpts = new BoundedChannelOptions(_pipeline.ChannelCapacity)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            };

            for (int i = 0; i < parts; i++)
            {
                var channel = Channel.CreateBounded<BufferSegment>(channelOpts);
                channels[i] = channel;

                string path = PathUtils.GetPartPath(_storage.OutputPathPattern, i);
                var writer = new FileWriter(path, _storage.BufferSize);

                var logger = Serilog.Log.ForContext("Partition", i);

                var consumer = new BatchConsumer(
                    writer,
                    Microsoft.Extensions.Options.Options.Create(_pipeline),
                    logger);

                consumers[i] = consumer
                    .ConsumeAsync(channel.Reader, ct)
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                            logger.Error(t.Exception, "Partition {Partition} consumer failed", i);
                    }, TaskScheduler.Current);
            }

            return (channels, consumers);
        }
    }
}
