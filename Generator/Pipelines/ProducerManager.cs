using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Generator.Abstractions;
using Generator.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generator.Pipelines
{
    /// <summary>
    /// Manages a set of producer tasks that generate BufferSegments
    /// into a pool of bounded channels (round-robin assignment).
    /// </summary>
    internal sealed class ProducerManager : IProducerManager
    {
        private readonly IProducer _producer;
        private readonly StorageOptions _storage;
        private readonly PipelineOptions _pipeline;
        private readonly ILogger<ProducerManager> _log;

        public ProducerManager(
            IProducer producer,
            IOptions<StorageOptions> storageOptions,
            IOptions<PipelineOptions> pipelineOptions,
            ILogger<ProducerManager> log)
        {
            _producer = producer;
            _storage = storageOptions?.Value ?? throw new ArgumentNullException(nameof(storageOptions));
            _pipeline = pipelineOptions?.Value ?? throw new ArgumentNullException(nameof(pipelineOptions));
            _log = log;
        }

        /// <summary>
        /// Kicks off <see cref="_pipeline.WorkerCount"/> producer tasks, each tasked
        /// with writing ~equal share of the total size into a round-robin channel.
        /// </summary>
        public Task[] RunProducers(Channel<BufferSegment>[] channels, CancellationToken ct)
        {
            long totalBytes = SizeParser.Parse(_storage.TargetSize);
            int workerCount = _pipeline.WorkerCount;
            long bytesPerWorker = totalBytes / workerCount;
            int partitionCount = channels.Length;

            _log.LogInformation(
                "Starting {WorkerCount} producers, ~{BytesPerWorker:N0} bytes each",
                workerCount, bytesPerWorker);

            var tasks = new Task[workerCount];

            for (int workerId = 0; workerId < workerCount; workerId++)
            {
                int partitionIndex = workerId % partitionCount;
                var writer = channels[partitionIndex].Writer;

                tasks[workerId] = ProduceAsync(
                    writer,
                    bytesPerWorker,
                    ct,
                    workerId,
                    partitionIndex);
            }

            return tasks;
        }

        private async Task ProduceAsync(
            ChannelWriter<BufferSegment> writer,
            long bytesToWrite,
            CancellationToken ct,
            int workerId,
            int partitionIndex)
        {
            _log.LogDebug(
                "Producer {WorkerId} assigned to partition {PartitionIndex}",
                workerId, partitionIndex);

            try
            {
                await _producer
                    .ProduceAsync(writer, bytesToWrite, ct)
                    .ConfigureAwait(false);

                _log.LogDebug("Producer {WorkerId} completed successfully", workerId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                _log.LogWarning("Producer {WorkerId} was canceled", workerId);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Producer {WorkerId} failed", workerId);
                throw;
            }
        }

        /// <summary>
        /// Waits for all producers to finish, then completes all channels
        /// so downstream consumers can drain out remaining data.
        /// </summary>
        public async Task WaitProducersAsync(Task[] producerTasks, Channel<BufferSegment>[] channels)
        {
            try
            {
                await Task.WhenAll(producerTasks).ConfigureAwait(false);
                _log.LogInformation("All producers have finished");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "One or more producers encountered errors");
            }
            finally
            {
                foreach (var ch in channels)
                {
                    ch.Writer.Complete();
                }
                _log.LogDebug("All channels marked complete");
            }
        }
    }
}
