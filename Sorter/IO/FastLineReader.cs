using Sorter.Models;
using System.Text;

namespace Sorter.IO
{
    /// <summary>
    /// High-performance buffered line reader for UTF-8 encoded files.
    /// Optimized for sequential scan of large sorted files during merging.
    /// </summary>
    public sealed class FastLineReader : IDisposable
    {
        private readonly FileStream _fs;

        // Raw byte buffer for file input
        private readonly byte[] _buf = new byte[64 * 1024];
        private int _byteLen;

        // UTF-8 decoder and char buffer
        private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
        private readonly char[] _charBuffer = new char[64 * 1024];
        private int _charLen;
        private int _charPos;

        /// <summary>
        /// Opens the file in sequential-scan mode for fast read access.
        /// </summary>
        public FastLineReader(string path)
        {
            _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, _buf.Length, FileOptions.SequentialScan);
        }

        /// <summary>
        /// Attempts to read the next full line from the stream.
        /// Returns false on EOF.
        /// </summary>
        /// <param name="line">The full line read (without newline character).</param>
        /// <param name="key">Parsed key from the line, if valid.</param>
        public bool TryReadLine(out string? line, out Key key)
        {
            var sb = new StringBuilder();
            key = default;

            while (true)
            {
                // Refill char buffer if exhausted
                if (_charPos == _charLen)
                {
                    if (!FillCharBuffer())
                    {
                        line = null; 
                        return false; // EOF
                    }
                }

                char c = _charBuffer[_charPos++];

                if (c == '\n') break;
                if (c == '\r') continue;
                
                sb.Append(c);
            }

            line = sb.ToString();
            LineParser.MakeKey(line, out key);
            return true;
        }

        /// <summary>
        /// Decodes the next chunk of UTF-8 bytes into characters.
        /// Returns false on end of stream.
        /// </summary>
        private bool FillCharBuffer()
        {
            _byteLen = _fs.Read(_buf, 0, _buf.Length);
            
            if (_byteLen == 0) 
                return false; // EOF

            _charLen = _decoder.GetChars(_buf, 0, _byteLen, _charBuffer, 0);
            _charPos = 0;
            return true;
        }

        public void Dispose() => _fs.Dispose();
    }
}
