using System.Globalization;

namespace Generator.Options
{
    internal static class SizeParser
    {
        public static long Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("size is empty");

            value = value.Trim().ToUpperInvariant();
            long mul = 1;
            if (value.EndsWith("KB")) { mul = 1L << 10; value = value[..^2]; }
            else if (value.EndsWith("MB")) { mul = 1L << 20; value = value[..^2]; }
            else if (value.EndsWith("GB")) { mul = 1L << 30; value = value[..^2]; }
            else if (value.EndsWith("B")) { mul = 1; value = value[..^1]; }

            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num))
                throw new FormatException("Invalid size format");

            return num * mul;
        }
    }
}
