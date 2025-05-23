using Generator.Abstractions;
using Generator.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generator.Validation
{
    /// <summary>
    /// Validates configuration and available resources on startup,
    /// then waits for user confirmation before allowing the host to continue.
    /// </summary>
    internal sealed class StartupValidator : IHostedService
    {
        private readonly ILogger<StartupValidator> _log;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly StorageOptions _storage;
        private readonly PipelineOptions _pipeline;
        private readonly IWordProvider _provider;

        public StartupValidator(
            IOptions<StorageOptions> storage,
            IOptions<PipelineOptions> pipeline,
            IWordProvider provider,
            IHostApplicationLifetime lifetime,
            ILogger<StartupValidator> log)
        {
            _storage = storage?.Value ?? throw new ArgumentNullException(nameof(storage));
            _pipeline = pipeline?.Value ?? throw new ArgumentNullException(nameof(pipeline));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _lifetime = lifetime ?? throw new ArgumentNullException(nameof(lifetime));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var words = _provider.LoadWords();
                if (words is null || words.Length == 0)
                    throw new ApplicationException("Word list is empty or cannot be loaded.");

                long targetBytes = SizeParser.Parse(_storage.TargetSize);
                var root = Path.GetPathRoot(Path.GetFullPath(_storage.OutputPath))!;
                var drive = new DriveInfo(root);
                double freeGb = drive.AvailableFreeSpace / 1e9;

                if (drive.AvailableFreeSpace < targetBytes * 1.05)
                    throw new ApplicationException(
                        $"Not enough free disk space on {root}. Required: {targetBytes / 1e9:F1} GB, " +
                        $"Available: {freeGb:F1} GB.");

                _log.LogInformation("===== Generator Configuration =====");
                _log.LogInformation("  OutputPath        : {Path}", _storage.OutputPath);
                _log.LogInformation("  OutputPathPattern : {Pattern}", _storage.OutputPathPattern);
                _log.LogInformation("  PartitionCount    : {Count}", _storage.PartitionCount);
                _log.LogInformation("  TargetSize        : {Size}", _storage.TargetSize);
                _log.LogInformation("  BufferSize        : {Bytes:N0} bytes", _storage.BufferSize);
                _log.LogInformation("  WordListPath      : {Path}", _storage.WordListPath);
                _log.LogInformation("  MaxNumber         : {Max}", _storage.MaxNumber);
                _log.LogInformation("  DuplicatePercentage: {Percent}%", _pipeline.DuplicatePercentage);
                _log.LogInformation("  WorkerCount       : {Workers}", _pipeline.WorkerCount);
                _log.LogInformation("  ChannelCapacity   : {Capacity}", _pipeline.ChannelCapacity);
                _log.LogInformation("  BatchWrite        : {Batch}", _pipeline.BatchWrite);
                _log.LogInformation("  AvailableDisk     : {FreeGb:F1} GB", freeGb);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Press <Enter> to start generation, or Ctrl+C to cancel...");
                Console.ResetColor();

                await WaitForEnterAsync(cancellationToken);

                _log.LogInformation(
                    "Startup checks passed. {WordCount} words loaded, free space {FreeGb:F1} GB.",
                    words.Length, freeGb);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log.LogWarning("Startup cancelled by user or host shutdown.");
                _lifetime.StopApplication();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Startup validation failed.");
                _lifetime.StopApplication();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static Task WaitForEnterAsync(CancellationToken token) =>
            Task.Run(() => Console.ReadLine(), token);
    }
}
