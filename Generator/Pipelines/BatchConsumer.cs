using System.Buffers;
using System.Threading.Channels;
using Generator.Abstractions;
using Generator.Options;
using Microsoft.Extensions.Options;
using Polly;
using Serilog;

namespace Generator.Pipelines
{
    /// <summary>
    /// Buffers incoming segments, writes them in batches to disk with retry logic,
    /// and periodically flushes according to time or batch size.
    /// </summary>
    internal sealed class BatchConsumer : IConsumer, IAsyncDisposable
    {
        private readonly IFileWriter _writer;
        private readonly PipelineOptions _options;
        private readonly ILogger _log;
        private readonly IAsyncPolicy _ioRetry;
        private readonly TimeSpan _flushInterval;
        private long _bytesWritten;

        public BatchConsumer(
            IFileWriter writer,
            IOptions<PipelineOptions> options,
            ILogger log)
        {
            _writer = writer;
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _log = log.ForContext<BatchConsumer>();
            _flushInterval = TimeSpan.FromSeconds(_options.FlushIntervalSec);

            _ioRetry = Policy
                .Handle<IOException>()
                .WaitAndRetryAsync(_options.RetryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (ex, _, _, _) => _log.Warning(ex, "I/O retry"));
        }

        public async Task ConsumeAsync(
            ChannelReader<BufferSegment> reader,
            CancellationToken cancellationToken)
        {
            var batch = new List<BufferSegment>(_options.BatchWrite);
            var nextFlushDue = DateTime.UtcNow + _flushInterval;

            try
            {
                await foreach (var segment in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    batch.Add(segment);

                    if (batch.Count >= _options.BatchWrite || DateTime.UtcNow >= nextFlushDue)
                    {
                        await FlushBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                        nextFlushDue = DateTime.UtcNow + _flushInterval;
                    }
                }

                if (batch.Count > 0)
                    await FlushBatchAsync(batch, cancellationToken).ConfigureAwait(false);

                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log.Warning("BatchConsumer canceled by token.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unhandled exception in BatchConsumer.");
                throw;
            }
            finally
            {
                await DisposeAsync().ConfigureAwait(false);
                _log.Information("BatchConsumer completed: {BytesWritten:N0} bytes written.", _bytesWritten);
            }
        }

        private async Task FlushBatchAsync(
            List<BufferSegment> segments,
            CancellationToken ct)
        {
            if (segments.Count == 0) return;

            foreach (var seg in segments)
                await _ioRetry.ExecuteAsync(
                    c => _writer.WriteAsync(seg.Buffer.AsMemory(..seg.Length), c), ct);

            _bytesWritten += segments.Sum(s => s.Length);

            foreach (var seg in segments)
                ArrayPool<byte>.Shared.Return(seg.Buffer);

            segments.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            await _writer.DisposeAsync().ConfigureAwait(false);
        }
    }
}
