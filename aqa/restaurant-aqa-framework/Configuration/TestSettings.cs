namespace Restaurant.Aqa.Configuration;

public sealed record TestSettings(Uri ApiBaseUrl, Uri? UiBaseUrl, string Region, string AccountId,
    string UserPoolId, IReadOnlyDictionary<string, string> Tables, string ResultsDirectory)
{
    public static TestSettings Load(bool requireUi = false)
    {
        LoadDotEnv();
        string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value : throw new InvalidOperationException($"Missing {name}. Configure .env or CI variables; live tests are not skipped.");
        if (Required("AQA_ALLOW_FIXTURE_WRITES") != "true")
            throw new InvalidOperationException("AQA_ALLOW_FIXTURE_WRITES must explicitly be true for isolated live fixtures.");
        static Uri Target(string name, string value)
        {
            if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps && !(uri.IsLoopback && uri.Scheme == Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException($"{name} must be an HTTPS base URL (HTTP allowed only for loopback), without credentials, query or fragment.");
            return uri;
        }
        var api = Target("API_BASE_URL", Required("API_BASE_URL"));
        var uiValue = Environment.GetEnvironmentVariable("UI_BASE_URL");
        var ui = string.IsNullOrWhiteSpace(uiValue) ? null : Target("UI_BASE_URL", uiValue);
        if (requireUi && ui is null) throw new InvalidOperationException("UI_BASE_URL is required for UI tests.");
        var tables = new[] { "Locations", "Tables", "Dishes", "Reservations", "Groups", "Sessions", "Orders", "Waiters" }
            .ToDictionary(name => name, name => Required("AQA_TABLE_" + name.ToUpperInvariant()));
        var directory = Path.GetFullPath(Environment.GetEnvironmentVariable("AQA_RESULTS_DIR") ?? "test-results");
        Directory.CreateDirectory(directory);
        return new TestSettings(api, ui, Required("AWS_REGION"), Required("AWS_ACCOUNT_ID"),
            Required("COGNITO_USER_POOL_ID"), tables, directory);
    }

    public static void LoadDotEnv()
    {
        var explicitPath = Environment.GetEnvironmentVariable("AQA_ENV_FILE");
        var path = explicitPath ?? Path.Combine(Directory.GetCurrentDirectory(), ".env");
        if (!File.Exists(path)) return;
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var split = trimmed.IndexOf('=');
            if (split <= 0) throw new InvalidOperationException("Invalid .env assignment. Use KEY=value, one per line.");
            var key = trimmed[..split].Trim();
            var value = trimmed[(split + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '\'' && value[^1] == '\'' || value[0] == '"' && value[^1] == '"'))
                value = value[1..^1];
            if (Environment.GetEnvironmentVariable(key) is null) Environment.SetEnvironmentVariable(key, value);
        }
    }
}
