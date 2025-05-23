using Sorter.Buffers;
using Sorter.Models;
using Sorter.Options;
using System.Buffers;
using System.Text;
using System.Threading.Channels;
using Sorter.IO;

namespace Sorter.Engines.ExternalMerge
{
    /// <summary>
    /// Reads a large input file line-by-line, parses and packs it into fixed-size memory chunks.
    /// Produces <see cref="Chunk"/> objects to be sorted and flushed independently.
    /// </summary>
    internal class ChunkProducer : IChunkProducer
    {
        public async Task ProduceAsync(
            SorterOptions sortOptions,
            FixedBufferPool pool,
            ChannelWriter<Chunk> writer,
            CancellationToken ct)
        {
            // Rent output buffer from pool (used for packing text in memory)
            char[] textBuf = await pool.RentAsync(ct);
            int used = 0;
            var entries = new List<Entry>(64_000);

            const int readBufSize = 128 * 1024;
            char[] readBuf = ArrayPool<char>.Shared.Rent(readBufSize);
            int readLen = 0, pos = 0;

            await using var fs = new FileStream(
                sortOptions.InputPath, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read,
                bufferSize: 1 << 20,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            using var reader = new StreamReader(
                fs, 
                Encoding.UTF8, 
                detectEncodingFromByteOrderMarks: true, 
                bufferSize: readBufSize);

            // Main file reading loop (reads 1 line at a time)
            while (true)
            {
                if (pos == readLen)
                {
                    readLen = await reader.ReadAsync(readBuf.AsMemory(), ct);
                    pos = 0;
                    if (readLen == 0) 
                        break; // EOF
                }

                int start = pos;
                int lf = Array.IndexOf(readBuf, '\n', start, readLen - start);

                if (lf == -1)
                {
                    // Incomplete line — shift tail to beginning and read more
                    int tail = readLen - start;
                    if (tail > 0)
                        Array.Copy(readBuf, start, readBuf, 0, tail);
                    
                    readLen = tail;
                    pos = tail;
                    continue;
                }

                int end = lf;
                if (end > 0 && readBuf[end - 1] == '\r')
                    end--; // Handle Windows-style CRLF

                ReadOnlySpan<char> line = readBuf.AsSpan(start, end - start);

                int chunkIndex = 0;
                if (LineParser.ParseLine(line, out int num, out ReadOnlySpan<char> textSpan))
                {
                    int len = textSpan.Length;

                    if (used + len > pool.BufferChars)
                    {
                        string lineCopy = new(textSpan); // before await

                        // Buffer full — emit current chunk and start a new one
                        await writer.WriteAsync(new Chunk(textBuf, entries.ToArray(), used), ct);

                        textBuf = await pool.RentAsync(ct);
                        entries = new List<Entry>(entries.Capacity);

                        // Copy overflowed line into new buffer
                        lineCopy.AsSpan().CopyTo(textBuf);
                        
                        entries.Add(new Entry(num, 0, len));
                        used = len;
                    }
                    else
                    {
                        textSpan.CopyTo(textBuf.AsSpan(used));
                        entries.Add(new Entry(num, used, len));
                        used += len;
                    }
                }

                pos = lf + 1; // Move to start of next line
            }

            // Emit remaining data (if any)
            if (entries.Count > 0)
                await writer.WriteAsync(new Chunk(textBuf, entries.ToArray(), used), ct);
            else
                pool.Return(textBuf);

            writer.Complete();
            ArrayPool<char>.Shared.Return(readBuf);
        }
    }
}
