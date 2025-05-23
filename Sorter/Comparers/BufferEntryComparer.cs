using Sorter.Models;

namespace Sorter.Comparers
{
    /// <summary>
    /// Compares two <see cref="Entry"/> items based on their string slice within a shared buffer.
    /// Falls back to numeric comparison if text is equal.
    /// </summary>
    public sealed class BufferEntryComparer : IComparer<Entry>
    {
        private readonly char[] _buffer;

        /// <param name="buffer">
        /// The shared buffer that all entries slice into.
        /// Must remain alive for the duration of comparisons.
        /// </param>
        public BufferEntryComparer(char[] buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        public int Compare(Entry a, Entry b)
        {
            int cmp = MemoryExtensions.CompareTo(_buffer.AsSpan(a.Start, a.Length),
                _buffer.AsSpan(b.Start, b.Length),
                StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : a.Number.CompareTo(b.Number);
        }
    }
}
