namespace Restaurant.Aqa.Fixtures;

public sealed class FixtureOwnership(string prefix)
{
    public void EnsureOwned(string key, string? actualGroupId = null)
    {
        if (string.IsNullOrWhiteSpace(prefix) || !prefix.StartsWith("aqa-", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid fixture run prefix.");
        bool Own(string value) => value.StartsWith(prefix + "-", StringComparison.Ordinal);
        if (Own(key) || key.StartsWith("owner-" + prefix + "-", StringComparison.Ordinal)
            || key.StartsWith("group-" + prefix + "-", StringComparison.Ordinal)
            || actualGroupId is not null && Own(actualGroupId)) return;
        throw new InvalidOperationException("Refusing to mutate a resource outside this fixture run.");
    }
}
