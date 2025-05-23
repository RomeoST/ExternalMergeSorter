namespace Generator.Abstractions
{
    public interface IFileMerger
    {
        Task MergePartsAsync(CancellationToken ct);
    }
}
