using System.Text.RegularExpressions;

namespace Restaurant.Aqa.Api;

public sealed class DiagnosticRedactor
{
    private readonly HashSet<string> secrets = new(StringComparer.Ordinal);
    public void Remember(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return;
        secrets.Add(secret);
        secrets.Add(Uri.EscapeDataString(secret));
    }
    public string Sanitize(string text)
    {
        foreach (var secret in secrets.OrderByDescending(value => value.Length))
            text = text.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
        text = Regex.Replace(text, "(?i)(\\\"(?:password|confirmPassword|idToken|accessToken|sessionToken|refreshToken|authorization|sessionTokenHash|guestSessionTokenHash|reservationToken|inviteToken|token)\\\"\\s*:\\s*)\\\"(?:\\\\.|[^\\\"\\\\])*\\\"", "$1\"[REDACTED]\"");
        return Regex.Replace(text, @"(?i)Bearer\s+[A-Za-z0-9._~+/=\-]+", "Bearer [REDACTED]");
    }
}
