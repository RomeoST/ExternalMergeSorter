namespace Generator.Abstractions
{
    public interface IFileWriter
    {
        Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);
        Task FlushAsync(CancellationToken ct);
        ValueTask DisposeAsync();
    }
}
