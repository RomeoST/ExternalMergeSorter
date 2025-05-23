using Sorter.Models;

namespace Sorter.Comparers
{
    /// <summary>
    /// Compares <see cref="Key"/> objects by their text (case-insensitive),
    /// falling back to numeric comparison if the text is equal.
    /// </summary>
    public sealed class KeyComparer : IComparer<Key>
    {
        /// <summary>
        /// Singleton instance to avoid allocations.
        /// </summary>
        public static readonly KeyComparer Instance = new();

        private KeyComparer() {}

        public int Compare(Key a, Key b)
        {
            int cmp = string.Compare(a.Text, b.Text, StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : a.Num.CompareTo(b.Num);
        }
    }
}
