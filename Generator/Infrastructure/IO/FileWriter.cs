using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Generator.Abstractions;

namespace Generator.Infrastructure.IO
{
    /// <summary>
    /// Asynchronously writes byte buffers to a file, with support for disposal.
    /// </summary>
    internal sealed class FileWriter : IFileWriter, IAsyncDisposable
    {
        private readonly FileStream _fileStream;

        /// <summary>
        /// Creates a new <see cref="FileWriter"/> for the given path.
        /// Ensures the containing directory exists.
        /// </summary>
        /// <param name="path">Target file path.</param>
        /// <param name="bufferSize">Internal buffer size, in bytes.</param>
        /// <exception cref="ArgumentException">If <paramref name="path"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="bufferSize"/> is not positive.</exception>
        public FileWriter(string path, int bufferSize)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("File path must be provided", nameof(path));
            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            _fileStream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.ReadWrite | FileShare.Delete,
                BufferSize = bufferSize,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan
            });
        }

        /// <inheritdoc/>
        public Task WriteAsync(ReadOnlyMemory<byte> buf, CancellationToken ct) =>
            _fileStream.WriteAsync(buf, ct).AsTask();

        /// <inheritdoc/>
        public Task FlushAsync(CancellationToken ct) =>
            _fileStream.FlushAsync(ct);

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            // Ensure any buffered data is flushed
            await _fileStream.FlushAsync().ConfigureAwait(false);
            await _fileStream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
