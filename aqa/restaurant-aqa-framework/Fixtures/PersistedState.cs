using Restaurant.Aqa.Api;
using System.Text.Json;
using System.Security.Cryptography;
namespace Restaurant.Aqa.Fixtures;

public static class PersistedState
{
    public static string Fingerprint(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) { WriteCanonical(writer, value); }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    public static async Task VerifyUnchangedAsync(Func<Task<IReadOnlyDictionary<string, string>>> capture, Func<Task<ApiReply>> action, Action<ApiReply> validate)
    {
        var before = await capture(); validate(await action()); var after = await capture();
        Assert.That(after, Is.EquivalentTo(before), "Rejected action changed persisted fixture state (values shown as SHA-256 fingerprints).");
    }
}
