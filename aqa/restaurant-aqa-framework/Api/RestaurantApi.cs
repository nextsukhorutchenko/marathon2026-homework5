using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Restaurant.Aqa.Api;

public sealed record ApiReply(int Status, string Body)
{
    public T Read<T>() => JsonSerializer.Deserialize<T>(Body, RestaurantApi.Json)
        ?? throw new InvalidOperationException("Expected non-null JSON response.");
    public string Error => JsonNode.Parse(Body)?["error"]?.ToString()
        ?? JsonNode.Parse(Body)?["message"]?.ToString() ?? "";
}

public sealed class RestaurantApi(Uri baseUrl, DiagnosticRedactor redactor) : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient http = new() { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(60) };

    public async Task<ApiReply> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        redactor.Remember(token ?? "");
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request);
            return new ApiReply((int)response.StatusCode, await response.Content.ReadAsStringAsync());
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"HTTP {method} {redactor.Sanitize(path)} failed: {error.GetType().Name}. Mutation outcome may be unknown; no automatic retry.");
        }
    }

    public async Task<T> OkAsync<T>(HttpMethod method, string path, string? token = null, object? body = null)
    {
        var reply = await SendAsync(method, path, token, body);
        Expect(reply, 200, method + " " + path);
        return reply.Read<T>();
    }

    public void Expect(ApiReply reply, int status, string operation)
    {
        if (reply.Status == status) return;
        var detail = redactor.Sanitize(reply.Body);
        if (detail.Length > 1200) detail = detail[..1200];
        Assert.Fail($"{redactor.Sanitize(operation)}: expected HTTP {status}, got {reply.Status}. {detail}");
    }

    public async Task<T> EventuallyAsync<T>(Func<Task<T>> read, Func<T, bool> ready, string description)
    {
        var expires = DateTime.UtcNow.AddSeconds(25);
        do
        {
            var value = await read();
            if (ready(value)) return value;
            await Task.Delay(500);
        } while (DateTime.UtcNow < expires);
        throw new AssertionException("Timed out waiting for " + description + ". Only read requests were repeated.");
    }

    public void Dispose() => http.Dispose();
}
