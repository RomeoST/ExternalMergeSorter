namespace Sorter.Services
{
    /// <summary>
    /// Coordinates the deferred start of a sorting operation.
    /// Allows background services to await external start signal (e.g. after user confirmation).
    /// </summary>
    public sealed class SortingCoordinator
    {
        // Used to block execution until start is signaled
        private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Asynchronously waits for the start signal.
        /// Throws <see cref="OperationCanceledException"/> if canceled.
        /// </summary>
        public Task WaitAsync(CancellationToken ct) => _tcs.Task.WaitAsync(ct);

        /// <summary>
        /// Signals that sorting should begin.
        /// Unblocks any awaiting tasks.
        /// </summary>
        public void SignalStart() => _tcs.TrySetResult();
    }
}
