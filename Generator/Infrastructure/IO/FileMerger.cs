using Generator.Abstractions;
using Generator.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Generator.Infrastructure.IO
{
    internal sealed class FileMerger : IFileMerger
    {
        private readonly StorageOptions _opts;
        private readonly ILogger<FileMerger> _log;

        public FileMerger(
            IOptions<StorageOptions> opts,
            ILogger<FileMerger> log)
        {
            _opts = opts?.Value ?? throw new ArgumentNullException(nameof(opts));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public async Task MergePartsAsync(CancellationToken ct)
        {
            int parts = _opts.PartitionCount;
            if (parts <= 0)
            {
                _log.LogWarning("No partitions to merge (PartitionCount = {Count})", parts);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_opts.OutputPath) ?? "");

            _log.LogInformation("Starting merge of {Parts} parts ->> {Output}",
                                parts, _opts.OutputPath);

            var outOpts = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = _opts.BufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            };

            await using var outFs = new FileStream(_opts.OutputPath, outOpts);

            for (int i = 0; i < parts; i++)
            {
                ct.ThrowIfCancellationRequested();

                string partPath = PathUtils.GetPartPath(_opts.OutputPathPattern, i);
                if (!File.Exists(partPath))
                {
                    _log.LogWarning("Partition {Index} not found: {Path}", i, partPath);
                    continue;
                }

                _log.LogDebug("Appending partition {Index}: {Path}", i, partPath);

                var inOpts = new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite | FileShare.Delete,
                    BufferSize = _opts.BufferSize,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                };

                await using var inFs = new FileStream(partPath, inOpts);
                await inFs.CopyToAsync(outFs, _opts.BufferSize, ct).ConfigureAwait(false);

                try
                {
                    await FileUtils.DeleteWithRetryAsync(partPath, _log, cancellationToken: ct)
                                  .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to delete partition {Path}", partPath);
                }
            }

            _log.LogInformation("Merge complete. Final output at {OutputPath}", _opts.OutputPath);
        }
    }
}
