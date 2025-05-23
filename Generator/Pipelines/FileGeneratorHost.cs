using Generator.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Generator.Pipelines
{
    /// <summary>
    /// Orchestrates the end-to-end file generation process:
    /// 1. Spins up consumers to write partitions in parallel.
    /// 2. Launches producers to feed data into those partitions.
    /// 3. Waits for all work to complete and merges the parts into the final file.
    /// </summary>
    internal sealed class FileGeneratorHost : BackgroundService
    {
        private readonly ILogger<FileGeneratorHost> _log;
        private readonly IProducerManager _producerManager;
        private readonly IConsumerManager _consumerManager;
        private readonly IFileMerger _fileMerger;
        private readonly IHostApplicationLifetime _lifetime;

        public FileGeneratorHost(
            IProducerManager producerManager,
            IConsumerManager consumerManager,
            IFileMerger fileMerger,
            IHostApplicationLifetime lifetime,
            ILogger<FileGeneratorHost> log)
        {
            _producerManager = producerManager;
            _consumerManager = consumerManager;
            _fileMerger = fileMerger;
            _lifetime = lifetime;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("Starting file generation…");

            try
            {
                var (channels, consumerTasks) = _consumerManager.CreateConsumers(stoppingToken);
                var producerTasks = _producerManager.RunProducers(channels, stoppingToken);

                await _producerManager.WaitProducersAsync(producerTasks, channels)
                                      .ConfigureAwait(false);

                await Task.WhenAll(consumerTasks).ConfigureAwait(false);

                await _fileMerger.MergePartsAsync(stoppingToken).ConfigureAwait(false);
                _log.LogInformation("File generation completed successfully.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning("File generation was canceled by user or shutdown.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "An unexpected error occurred during file generation.");
            }
            finally
            {
                _log.LogInformation("Shutting down host.");
                _lifetime.StopApplication();
            }
        }
    }
}
