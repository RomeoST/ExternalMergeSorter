namespace Sorter.Engines.ExternalMerge
{
    public interface IMerger
    {
        Task MergeAsync(
            IReadOnlyList<string> runs,
            string outputPath,
            int degree,
            CancellationToken ct);
    }
}
