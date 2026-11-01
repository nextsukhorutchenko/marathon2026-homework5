namespace Restaurant.Aqa.Fixtures;

public sealed class CleanupLedger
{
    private readonly List<(string Id, Func<Task> Action)> operations = [];
    private readonly HashSet<string> registered = new(StringComparer.Ordinal);
    private bool finished;
    public void Register(string resourceId, Func<Task> cleanup)
    {
        if (finished) throw new InvalidOperationException("Cannot register after cleanup has finished.");
        if (registered.Add(resourceId)) operations.Add((resourceId, cleanup));
    }
    public async Task RunAsync()
    {
        if (finished) return;
        finished = true;
        var errors = new List<Exception>();
        foreach (var operation in operations.AsEnumerable().Reverse())
        {
            try { await operation.Action(); }
            catch (Exception error) { errors.Add(new InvalidOperationException($"Cleanup failed for {operation.Id}: {error.GetType().Name}.")); }
        }
        if (errors.Count > 0) throw new AggregateException("Fixture cleanup incomplete; see recovery ledger.", errors);
    }
}
