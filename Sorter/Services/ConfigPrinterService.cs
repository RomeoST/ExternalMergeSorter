using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorter.Options;

namespace Sorter.Services
{
    /// <summary>
    /// Prints current configuration on startup, 
    /// waits for user to press Enter, then signals the sorting coordinator.
    /// </summary>
    internal sealed class ConfigPrinterService : IHostedService
    {
        private readonly ILogger<ConfigPrinterService> _log;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly SorterOptions _opts;
        private readonly SortingCoordinator _coordinator;
        private CancellationTokenRegistration? _shutdownRegistration;

        public ConfigPrinterService(
            ILogger<ConfigPrinterService> log,
            IHostApplicationLifetime lifetime,
            IOptions<SorterOptions> opts,
            SortingCoordinator coordinator)
        {
            _log = log;
            _lifetime = lifetime;
            _opts = opts.Value;
            _coordinator = coordinator;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _log.LogInformation(
                "Sorter configuration:\n" +
                "  {Property,-15}: {Value}\n" +
                "  {Property2,-15}: {Value2}\n" +
                "  {Property3,-15}: {Value3}\n" +
                "  {Property4,-15}: {Value4} MB\n" +
                "  {Property5,-15}: {Value5}",
                "InputPath", _opts.InputPath,
                "OutputPath", _opts.OutputPath,
                "TempDirectory", _opts.TempDirectory,
                "ChunkSizeMb", _opts.ChunkSizeMb,
                "Degree", _opts.Degree
            );
            _log.LogInformation("Press <Enter> to begin sorting, or Ctrl+C to abort.");

            _shutdownRegistration = _lifetime.ApplicationStopping
                .Register(() => _log.LogWarning("Shutdown requested before start. Aborting prompt."));

            _ = PromptAndSignalStartAsync(cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _shutdownRegistration?.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Waits for the user to press Enter, then signals the coordinator.
        /// Honors both the host stopping and CTRL+C via the cancellation token.
        /// </summary>
        private async Task PromptAndSignalStartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Run(() => Console.ReadLine(), cancellationToken);

                _coordinator.SignalStart();
                _log.LogInformation("Start signal sent to SortingCoordinator.");
            }
            catch (OperationCanceledException)
            {
                // Cancellation may come from CTRL+C or host shutdown
                _log.LogWarning("Prompt cancelled; application is shutting down.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Unexpected error while waiting for start input.");
                _lifetime.StopApplication();
            }
        }
    }
}
