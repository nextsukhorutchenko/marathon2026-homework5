using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using System.Globalization;
using System.Text.Json;
using Restaurant.Aqa.Configuration;

namespace Restaurant.Aqa.Fixtures;

public sealed record TrackedRow(string Logical, Dictionary<string, string> Key, string? Relation, string? RelatedId, bool Seeded);

public sealed class AwsFixtureStore : IAsyncDisposable
{
    private readonly TestSettings settings;
    private readonly AmazonDynamoDBClient database;
    private readonly AmazonCognitoIdentityProviderClient cognito;
    private readonly AmazonSecurityTokenServiceClient security;
    private readonly CleanupLedger cleanup = new();
    private readonly List<TrackedRow> rows = [];
    private readonly List<string> identities = [];
    private readonly HashSet<string> confirmedSeeds = new(StringComparer.Ordinal);
    private readonly PendingGuestJoins guestJoins = new();
    private readonly FixtureOwnership ownership;
    private bool disposed;
    public string Prefix { get; } = "aqa-" + Guid.NewGuid().ToString("N");
    public string DirectoryPath { get; }
    private static readonly Dictionary<string, string[]> Keys = new()
    {
        ["Locations"] = ["id"],
        ["Tables"] = ["id"],
        ["Dishes"] = ["id"],
        ["Reservations"] = ["id"],
        ["Groups"] = ["groupReservationId"],
        ["Sessions"] = ["guestSessionId"],
        ["Orders"] = ["orderId", "customerId"],
        ["Waiters"] = ["userId"]
    };

    public AwsFixtureStore(TestSettings configuration)
    {
        settings = configuration;
        var region = RegionEndpoint.GetBySystemName(settings.Region);
        database = new AmazonDynamoDBClient(region);
        cognito = new AmazonCognitoIdentityProviderClient(region);
        security = new AmazonSecurityTokenServiceClient(region);
        ownership = new FixtureOwnership(Prefix);
        DirectoryPath = Path.Combine(settings.ResultsDirectory, Prefix);
        Directory.CreateDirectory(DirectoryPath);
    }

    public async Task InitializeAsync()
    {
        var caller = await security.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        if (caller.Account != settings.AccountId) throw new InvalidOperationException("AWS account does not match AWS_ACCOUNT_ID; no fixture writes performed.");
        await SaveLedgerAsync("initializing");
    }

    public async Task PutAsync(string logical, Dictionary<string, object> values)
    {
        var key = Keys[logical].ToDictionary(name => name, name => Convert.ToString(values[name], CultureInfo.InvariantCulture)!);
        ownership.EnsureOwned(key.Values.First(), values.GetValueOrDefault("locationId") as string);
        await TrackAsync(new TrackedRow(logical, key, null, null, true));
        values["aqaRunId"] = Prefix;
        var item = JsonSerializer.SerializeToElement(values).EnumerateObject().ToDictionary(property => property.Name, property => Attribute(property.Value));
        await database.PutItemAsync(new PutItemRequest
        {
            TableName = settings.Tables[logical],
            Item = item,
            ConditionExpression = "attribute_not_exists(#primary)",
            ExpressionAttributeNames = new Dictionary<string, string> { { "#primary", Keys[logical][0] } }
        });
        confirmedSeeds.Add(ResourceId(logical, key));
    }

    public async Task TrackRelatedAsync(string logical, Dictionary<string, string> key, string relation, string relatedId)
    {
        ownership.EnsureOwned(relatedId);
        await TrackAsync(new TrackedRow(logical, key, relation, relatedId, false));
    }

    public Task TrackGeneratedAsync(string logical, Dictionary<string, string> key) =>
        TrackAsync(new TrackedRow(logical, key, null, null, false));

    private async Task TrackAsync(TrackedRow row)
    {
        var resource = ResourceId(row.Logical, row.Key);
        if (rows.Any(existing => existing.Logical == row.Logical && existing.Key.OrderBy(pair => pair.Key).SequenceEqual(row.Key.OrderBy(pair => pair.Key)))) return;
        rows.Add(row);
        cleanup.Register(resource, async () =>
        {
            var item = await GetAsync(row.Logical, row.Key);
            if (item.Count == 0) return;
            if (row.Seeded)
            {
                var marker = item.GetValueOrDefault("aqaRunId")?.S;
                if (marker is not null && marker != Prefix || marker is null && !confirmedSeeds.Contains(resource))
                    throw new InvalidOperationException("Creation ownership was not confirmed; refusing deletion.");
            }
            else if (row.Relation is not null)
            {
                var actual = item.GetValueOrDefault(row.Relation)?.S;
                if (actual != row.RelatedId) throw new InvalidOperationException("Stored relationship does not match fixture; refusing deletion.");
                ownership.EnsureOwned(row.Key.Values.First(), actual);
            }
            else ownership.EnsureOwned(row.Key.Values.First());
            await database.DeleteItemAsync(new DeleteItemRequest { TableName = settings.Tables[row.Logical], Key = Key(row.Key) });
            if ((await GetAsync(row.Logical, row.Key)).Count != 0) throw new InvalidOperationException("Fixture row remains after deletion.");
        });
        await SaveLedgerAsync("active");
    }

    private static string ResourceId(string logical, Dictionary<string, string> key) => logical + ":" + string.Join("/", key.OrderBy(pair => pair.Key).Select(pair => pair.Value));

    public async Task<string> BeginGuestJoinAsync(string groupId)
    {
        ownership.EnsureOwned(groupId);
        var attempt = guestJoins.Begin(groupId);
        cleanup.Register("unknown-guest-response:" + groupId, async () =>
        {
            if (!guestJoins.Attempts.Values.Contains(groupId, StringComparer.Ordinal)) return;
            // A timed-out join can have succeeded remotely. Only this fixture's group is queried.
            Dictionary<string, AttributeValue>? lastKey = null;
            for (var poll = 0; poll < 4; poll++)
            {
                do
                {
                    var page = await database.QueryAsync(new QueryRequest
                    {
                        TableName = settings.Tables["Sessions"],
                        IndexName = "groupReservationId",
                        KeyConditionExpression = "groupReservationId = :group",
                        ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":group", new() { S = groupId } } },
                        ExclusiveStartKey = lastKey
                    });
                    foreach (var indexed in page.Items ?? [])
                    {
                        var key = new Dictionary<string, string> { { "guestSessionId", indexed["guestSessionId"].S } };
                        var actual = await GetAsync("Sessions", key);
                        if (actual.Count == 0) continue;
                        ownership.EnsureOwned(key.Values.First(), actual.GetValueOrDefault("groupReservationId")?.S);
                        if (actual.GetValueOrDefault("groupReservationId")?.S != groupId) throw new InvalidOperationException("Recovery relationship mismatch.");
                        await database.DeleteItemAsync(new DeleteItemRequest { TableName = settings.Tables["Sessions"], Key = Key(key) });
                        if ((await GetAsync("Sessions", key)).Count > 0) throw new InvalidOperationException("Recovered session cleanup incomplete.");
                    }
                    lastKey = page.LastEvaluatedKey;
                } while (lastKey is { Count: > 0 });
                await Task.Delay(500);
            }
            // Even an empty eventual GSI cannot prove an unknown remote mutation did not succeed.
            // Keep the attempt in the durable journal and mark cleanup incomplete for manual recovery.
            guestJoins.RequireVerified(groupId);
        });
        await SaveLedgerAsync("active");
        return attempt;
    }

    public async Task CompleteGuestJoinAsync(string attempt)
    { guestJoins.Complete(attempt); await SaveLedgerAsync("active"); }

    public async Task<IReadOnlyDictionary<string, string>> CaptureStateAsync()
    {
        var state = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var item = await GetAsync(row.Logical, row.Key);
            // Only fingerprints reach NUnit diagnostics; session-token hashes stay out of artifacts.
            using var json = JsonDocument.Parse(Amazon.DynamoDBv2.DocumentModel.Document.FromAttributeMap(item).ToJson());
            state.Add(ResourceId(row.Logical, row.Key), PersistedState.Fingerprint(json.RootElement));
        }
        return state;
    }

    public async Task<Dictionary<string, AttributeValue>> GetAsync(string logical, Dictionary<string, string> key)
    {
        var response = await database.GetItemAsync(new GetItemRequest { TableName = settings.Tables[logical], Key = Key(key), ConsistentRead = true });
        return response.Item ?? [];
    }

    public async Task DeleteOwnedSeedAsync(string logical, Dictionary<string, string> key)
    {
        var item = await GetAsync(logical, key);
        if (item.GetValueOrDefault("aqaRunId")?.S != Prefix) throw new InvalidOperationException("Only this run's directly seeded row may be removed for a negative case.");
        await database.DeleteItemAsync(new DeleteItemRequest { TableName = settings.Tables[logical], Key = Key(key) });
    }

    public async Task ReserveIdentityAsync(string email)
    {
        ownership.EnsureOwned(email);
        try
        {
            await cognito.AdminGetUserAsync(new AdminGetUserRequest { UserPoolId = settings.UserPoolId, Username = email });
            throw new InvalidOperationException("Fixture identity already exists; refusing to adopt it.");
        }
        catch (UserNotFoundException) { }
        identities.Add(email);
        cleanup.Register("cognito:" + email, async () =>
        {
            ownership.EnsureOwned(email);
            try { await cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest { UserPoolId = settings.UserPoolId, Username = email }); }
            catch (UserNotFoundException) { }
            try
            {
                await cognito.AdminGetUserAsync(new AdminGetUserRequest { UserPoolId = settings.UserPoolId, Username = email });
                throw new InvalidOperationException("Fixture Cognito identity remains after deletion.");
            }
            catch (UserNotFoundException) { }
        });
        await SaveLedgerAsync("active");
    }

    public async Task MakeWaiterAsync(string email)
    {
        ownership.EnsureOwned(email);
        if (!identities.Contains(email)) throw new InvalidOperationException("Only an identity created by this fixture can change role.");
        await cognito.AdminAddUserToGroupAsync(new AdminAddUserToGroupRequest { UserPoolId = settings.UserPoolId, Username = email, GroupName = "Waiter" });
        await cognito.AdminRemoveUserFromGroupAsync(new AdminRemoveUserFromGroupRequest { UserPoolId = settings.UserPoolId, Username = email, GroupName = "Customer" });
    }

    private Task SaveLedgerAsync(string state) => File.WriteAllTextAsync(Path.Combine(DirectoryPath, "cleanup-ledger.json"), JsonSerializer.Serialize(new { prefix = Prefix, state, rows, identities, pendingGuestJoins = guestJoins.Attempts }, new JsonSerializerOptions { WriteIndented = true }));

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        try { await cleanup.RunAsync(); await SaveLedgerAsync("cleaned-and-verified"); }
        catch { await SaveLedgerAsync("cleanup-failed"); throw; }
        finally { database.Dispose(); cognito.Dispose(); security.Dispose(); }
    }

    private static Dictionary<string, AttributeValue> Key(Dictionary<string, string> key) => key.ToDictionary(pair => pair.Key, pair => new AttributeValue { S = pair.Value });
    private static AttributeValue Attribute(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => new() { S = element.GetString() },
        JsonValueKind.Number => new() { N = element.GetRawText() },
        JsonValueKind.True => new() { BOOL = true },
        JsonValueKind.False => new() { BOOL = false },
        JsonValueKind.Null => new() { NULL = true },
        JsonValueKind.Array => new() { L = element.EnumerateArray().Select(Attribute).ToList() },
        JsonValueKind.Object => new() { M = element.EnumerateObject().ToDictionary(property => property.Name, property => Attribute(property.Value)) },
        _ => throw new InvalidOperationException("Unsupported fixture value.")
    };
}
