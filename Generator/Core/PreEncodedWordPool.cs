using Generator.Abstractions;
using System.Runtime.CompilerServices;
using System.Text;

namespace Generator.Core
{
    public sealed class PreEncodedWordPool
    {
        private readonly byte[][] _words;
        private readonly byte[][] _dup;
        public PreEncodedWordPool(IWordProvider p)
        {
            var w = p.LoadWords();
            _words = w.Select(Encoding.UTF8.GetBytes).ToArray();
            var rnd = new Random(Environment.TickCount ^ Environment.ProcessId);
            _dup = _words.OrderBy(_ => rnd.Next()).Take(Math.Max(1, _words.Length / 4)).ToArray();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public byte[] Random(Random r) => _words[r.Next(_words.Length)];
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public byte[] RandomDuplicate(Random r) => _dup[r.Next(_dup.Length)];
    }
}
