using Generator.Abstractions;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Buffers.Text;
using System.Threading.Channels;
using Generator.Options;

namespace Generator.Core
{
    public sealed class LineProducer : IProducer
    {
        private readonly PreEncodedWordPool _pool;
        private readonly PipelineOptions _pipelinesOptions;
        private readonly StorageOptions _storageOptions;

        public LineProducer(PreEncodedWordPool pool, IOptions<PipelineOptions> o, IOptions<StorageOptions> storageOptions)
        { 
            _pool = pool; 

            _pipelinesOptions = o.Value;
            _storageOptions = storageOptions.Value;
        }

        public async Task ProduceAsync(ChannelWriter<BufferSegment> writer, long targetBytes, CancellationToken ct)
        {
            var rnd = new Random(Environment.TickCount ^ Environment.ProcessId);
            long produced = 0;
            while (produced < targetBytes && !ct.IsCancellationRequested)
            {
                var word = rnd.Next(100) < _pipelinesOptions.DuplicatePercentage 
                    ? _pool.RandomDuplicate(rnd) 
                    : _pool.Random(rnd);

                int num = rnd.Next(1, _storageOptions.MaxNumber + 1);

                // 13 = maximum prefix length:
                // number (up to 10 digits) + ". " (2 bytes) + "\n" (1 byte)
                int needed = 13 + word.Length;

                byte[] buf = ArrayPool<byte>.Shared.Rent(needed);
                var span = buf.AsSpan();

                if (!Utf8Formatter.TryFormat(num, span, out int written))
                    throw new InvalidOperationException();

                span[written++] = (byte)'.';
                span[written++] = (byte)' ';
                word.CopyTo(span.Slice(written));
                written += word.Length;
                span[written++] = (byte)'\n';

                await writer.WriteAsync(new BufferSegment(buf, written), ct);
                produced += written;
            }
        }
    }
}
