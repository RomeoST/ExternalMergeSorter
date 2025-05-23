using Sorter.Options;

namespace Sorter.Abstractions
{
    public interface ISortingEngine
    {
        Task SortAsync(SorterOptions sorterOptions, CancellationToken ct);
    }
}
