using Sorter.Models;

namespace Sorter.IO
{
    internal static class LineParser
    {
        /// <summary>
        /// Parses a string in the format "{number}. {text}" into a number and text.
        /// </summary>
        /// <param name="line">The line to parse.</param>
        /// <param name="num">The parsed number.</param>
        /// <param name="text">The parsed text after the number and dot.</param>
        /// <returns>True if the parsing was successful.</returns>
        public static bool ParseLine(ReadOnlySpan<char> line, out int num, out ReadOnlySpan<char> text)
        {
            num = default;
            text = default;
            int dotIndex = line.IndexOf('.');
            if (dotIndex <= 0)
                return false;

            // Parse the number before the dot
            if (!int.TryParse(line.Slice(0, dotIndex), out num))
                return false;

            // Expect a space after the dot: ". "
            int textStart = dotIndex + 2;
            if (textStart > line.Length)
                return false;

            text = line.Slice(textStart);
            return true;
        }

        /// <summary>
        /// Forms a key for the heap from a string in the format "{number}. {text}".
        /// </summary>
        /// <param name="line">The line to parse.</param>
        /// <param name="key">The resulting key.</param>
        /// <returns>True if the key was successfully created.</returns>
        public static bool MakeKey(string line, out Key key)
        {
            key = default;
            int dotIndex = line.IndexOf('.');
            if (dotIndex <= 0)
                return false;

            // Parse the number before the dot
            if (!int.TryParse(line.AsSpan(0, dotIndex), out int num))
                return false;

            // Extract text after ". "
            string text = dotIndex + 2 < line.Length ? line.Substring(dotIndex + 2) : string.Empty;
            key = new Key(text, num);
            return true;
        }
    }
}
