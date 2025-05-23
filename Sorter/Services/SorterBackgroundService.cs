using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sorter.Abstractions;
using Sorter.Options;

namespace Sorter.Services
{
    public sealed class SorterBackgroundService : BackgroundService
    {
        private readonly ILogger<SorterBackgroundService> _log;
        private readonly SortingCoordinator _coord;
        private readonly IOptions<SorterOptions> _opts;
        private readonly ISortingEngine _engine;
        private readonly IHostApplicationLifetime _lifetime;

        public SorterBackgroundService(
            ILogger<SorterBackgroundService> log,
            SortingCoordinator coord,
            IOptions<SorterOptions> opts,
            ISortingEngine engine,
            IHostApplicationLifetime lifetime)
        {
            _log = log;
            _coord = coord;
            _opts = opts;
            _engine = engine;
            _lifetime = lifetime;
        }

        /// <summary>
        /// Main execution loop: waits for the start signal, runs the sorting engine,
        /// logs duration and handles graceful shutdown or errors.
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _log.LogInformation("Waiting for start signal...");
                await _coord.WaitAsync(stoppingToken);

                _log.LogInformation("Sorting started.");
                var stopwatch = Stopwatch.StartNew();

                await _engine.SortAsync(_opts.Value, stoppingToken);

                stopwatch.Stop();
                _log.LogInformation("Sorting completed in {Elapsed}.", stopwatch.Elapsed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _log.LogWarning("Sorting operation was canceled by the user.");
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "An error occurred during sorting.");
            }
            finally
            {
                // Allow time for logs to flush and any cleanup to finish
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);

                _log.LogInformation("Initiating application shutdown...");
                _lifetime.StopApplication();
            }
        }
    }
}
