using System.Threading.Channels;

namespace Sorter.Buffers
{
    /// <summary>
    /// Thread-safe pool of pre-allocated fixed-size <c>char[]</c> buffers.
    /// Used to minimize GC pressure during chunked text processing.
    /// </summary>
    public sealed class FixedBufferPool
    {
        private readonly Channel<char[]> _pool;

        /// <summary>
        /// Size of each buffer in characters.
        /// </summary>
        public int BufferChars { get; }

        /// <summary>
        /// Initializes the buffer pool with (degree + 1) reusable char arrays.
        /// </summary>
        /// <param name="bufferSize">Size of each char buffer.</param>
        /// <param name="degree">Number of expected consumers (e.g. threads).</param>
        public FixedBufferPool(int bufferSize, int degree)
        {
            if (bufferSize <= 0) throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (degree <= 0) throw new ArgumentOutOfRangeException(nameof(degree));

            BufferChars = bufferSize;
            _pool = Channel.CreateBounded<char[]>(degree + 1);   // One per worker + producer

            for (int i = 0; i <= degree; i++)
                _pool.Writer.TryWrite(new char[bufferSize]);
        }

        /// <summary>
        /// Rents a buffer from the pool. Waits if none are available.
        /// </summary>
        public ValueTask<char[]> RentAsync(CancellationToken ct) =>
            _pool.Reader.ReadAsync(ct);

        /// <summary>
        /// Returns a buffer to the pool. Ignores null or full-channel cases silently.
        /// </summary>
        public void Return(char[]? buffer)
        {
            if (buffer is { Length: > 0 })
            {
                _pool.Writer.TryWrite(buffer);
            }
        }
    }
}
