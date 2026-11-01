using Restaurant.Aqa.Api;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Restaurant.Aqa.Diagnostics;

public static class TraceArchive
{
    // Playwright stores network headers, snapshots and response bodies in separate ZIP entries.
    // Decode every UTF-8 entry, including extensionless resources; preserve binary screenshots.
    public static void Sanitize(string source, string target, DiagnosticRedactor redactor)
    {
        using var input = ZipFile.OpenRead(source);
        using var output = ZipFile.Open(target, ZipArchiveMode.Create);
        var utf8 = new UTF8Encoding(false, true);
        // Invitation credentials also occur in request paths and can be shared across trace entries.
        foreach (var network in input.Entries.Where(e => e.FullName.EndsWith(".network", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(network.Open(), utf8);
            foreach (Match match in Regex.Matches(reader.ReadToEnd(), @"/guest/invites/([A-Za-z0-9._~%\-]+)"))
            {
                redactor.Remember(match.Groups[1].Value);
                redactor.Remember(Uri.UnescapeDataString(match.Groups[1].Value));
            }
        }
        foreach (var entry in input.Entries)
        {
            using var stream = entry.Open(); using var buffer = new MemoryStream(); stream.CopyTo(buffer);
            var bytes = buffer.ToArray();
            try { bytes = utf8.GetBytes(redactor.Sanitize(utf8.GetString(bytes))); }
            catch (DecoderFallbackException) { /* Binary attachment, not a textual credential source. */ }
            using var destination = output.CreateEntry(entry.FullName).Open(); destination.Write(bytes);
        }
    }
}
