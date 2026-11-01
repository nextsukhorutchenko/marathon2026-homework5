namespace Restaurant.Aqa.Fixtures;

public sealed class PendingGuestJoins
{
    private readonly Dictionary<string, string> attempts = new();
    public IReadOnlyDictionary<string, string> Attempts => attempts;
    public string Begin(string group)
    {
        var id = Guid.NewGuid().ToString("N"); attempts.Add(id, group); return id;
    }
    public void Complete(string attempt)
    {
        if (!attempts.Remove(attempt)) throw new InvalidOperationException("Unknown guest join attempt.");
    }
    public void RequireVerified(string group)
    {
        if (attempts.Values.Contains(group, StringComparer.Ordinal))
            throw new InvalidOperationException("Guest join outcome unknown; eventual index recovery cannot prove complete cleanup. See pendingGuestJoins in recovery ledger.");
    }
}
