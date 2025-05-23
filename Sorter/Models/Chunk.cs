namespace Sorter.Models
{
    public readonly record struct Chunk(char[] Buffer, Entry[] Entries, int Used);
}
