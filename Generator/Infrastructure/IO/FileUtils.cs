using Microsoft.Extensions.Logging;

namespace Generator.Infrastructure.IO
{
    /// <summary>
    /// Utility methods for working with files, including safe delete with retries.
    /// </summary>
    public static class FileUtils
    {
        /// <summary>
        /// Attempts to delete the specified file, retrying on transient errors.
        /// </summary>
        /// <param name="path">Path of the file to delete.</param>
        /// <param name="logger">Logger for reporting retry attempts and failures.</param>
        /// <param name="maxRetries">Maximum number of retry attempts (default 4).</param>
        /// <param name="baseDelayMs">Base delay in milliseconds for exponential back-off (default 200ms).</param>
        /// <param name="cancellationToken">Cancellation token to abort retries.</param>
        public static async Task DeleteWithRetryAsync(
            string path,
            ILogger logger,
            int maxRetries = 4,
            int baseDelayMs = 200,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path must be provided", nameof(path));
            if (logger is null)
                throw new ArgumentNullException(nameof(logger));

            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    logger.LogDebug(ex, "IOException on deleting file '{Path}', retry {Attempt}/{MaxRetries}", path, attempt + 1, maxRetries);
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries)
                {
                    logger.LogDebug(ex, "UnauthorizedAccessException on deleting file '{Path}', retry {Attempt}/{MaxRetries}", path, attempt + 1, maxRetries);
                }

                int delay = baseDelayMs * (1 << attempt);
                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("DeleteWithRetryAsync cancelled before completing retries for '{Path}'", path);
                    return;
                }
            }

            logger.LogWarning("Failed to delete file '{Path}' after {MaxRetries} retries", path, maxRetries);
        }
    }
}
