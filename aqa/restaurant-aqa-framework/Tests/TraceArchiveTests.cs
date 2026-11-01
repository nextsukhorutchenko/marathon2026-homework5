using System.IO.Compression;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Diagnostics;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Infrastructure")]
public sealed class TraceArchiveTests
{
    [Test]
    public void Trace_archive_redacts_network_and_snapshot_resources_without_corrupting_images()
    {
        var root = Path.Combine(Path.GetTempPath(), "aqa-trace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "raw.zip"); var target = Path.Combine(root, "safe.zip");
            var png = new byte[] { 137, 80, 78, 71, 255, 0 };
            using (var zip = ZipFile.Open(source, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("trace.network").Open())) writer.Write("{\"value\":\"Bearer eyJ.trace.secret\",\"url\":\"https://example.test/guest/invites/private-invite-key\"}");
                using (var writer = new StreamWriter(zip.CreateEntry("resources/body").Open())) writer.Write("echo Aqa!password / diagnostic");
                using var image = zip.CreateEntry("resources/screenshot.png").Open(); image.Write(png);
            }
            var redactor = new DiagnosticRedactor(); redactor.Remember("Aqa!password");
            TraceArchive.Sanitize(source, target, redactor);
            using var clean = ZipFile.OpenRead(target);
            using var network = new StreamReader(clean.GetEntry("trace.network")!.Open());
            Assert.That(network.ReadToEnd(), Does.Not.Contain("eyJ.trace.secret").And.Not.Contain("private-invite-key").And.Contain("REDACTED"));
            using var body = new StreamReader(clean.GetEntry("resources/body")!.Open());
            Assert.That(body.ReadToEnd(), Is.EqualTo("echo [REDACTED] / diagnostic"));
            using var binary = clean.GetEntry("resources/screenshot.png")!.Open();
            using var bytes = new MemoryStream(); binary.CopyTo(bytes);
            Assert.That(bytes.ToArray(), Is.EqualTo(png));
        }
        finally { Directory.Delete(root, true); }
    }
}
