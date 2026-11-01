using Microsoft.Playwright;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Pages;

namespace Restaurant.Aqa.Diagnostics;

public sealed class UiSession : IAsyncDisposable
{
    private readonly IPlaywright playwright;
    private readonly IBrowser browser;
    private readonly IBrowserContext context;
    private readonly DiagnosticRedactor redactor;
    private readonly string directory;
    private bool tracing;
    public IPage Page { get; }
    private UiSession(IPlaywright playwright, IBrowser browser, IBrowserContext context, IPage page, DiagnosticRedactor redactor, string directory)
    { this.playwright = playwright; this.browser = browser; this.context = context; Page = page; this.redactor = redactor; this.directory = directory; }

    public static async Task<UiSession> CreateAsync(Uri baseUrl, DiagnosticRedactor redactor, string directory)
    {
        // Locator assertions have a separate 5s default; live page loading makes several sequential HTTP reads.
        Assertions.SetDefaultExpectTimeout(30000);
        var playwright = await Playwright.CreateAsync(); IBrowser? browser = null;
        try
        {
            browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var context = await browser.NewContextAsync(new() { BaseURL = baseUrl.ToString().TrimEnd('/'), Locale = "en-US", TimezoneId = "UTC", ViewportSize = new() { Width = 1440, Height = 1000 } });
            context.SetDefaultTimeout(30000);
            return new(playwright, browser, context, await context.NewPageAsync(), redactor, directory);
        }
        catch { if (browser is not null) await browser.DisposeAsync(); playwright.Dispose(); throw; }
    }
    public async Task LoginAsync(Principal principal, bool waiter = false)
    {
        await new LoginPage(Page, redactor).SignInAsync(principal, waiter);
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true, Sources = false }); tracing = true;
    }
    public async Task FailureArtifactsAsync()
    {
        Directory.CreateDirectory(directory);
        var screenshot = Path.Combine(directory, "failure.png");
        await Page.ScreenshotAsync(new() { Path = screenshot, FullPage = true }); TestContext.AddTestAttachment(screenshot, "UI failure");
        if (!tracing) return;
        // Raw traces are temporary and never published: headers can contain authentication data.
        var raw = Path.Combine(Path.GetTempPath(), "aqa-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            await context.Tracing.StopAsync(new() { Path = raw }); tracing = false;
            var safe = Path.Combine(directory, "trace.zip"); TraceArchive.Sanitize(raw, safe, redactor);
            TestContext.AddTestAttachment(safe, "Sanitized Playwright trace");
        }
        finally { if (File.Exists(raw)) File.Delete(raw); }
    }
    public async ValueTask DisposeAsync()
    {
        try { await context.CloseAsync(); }
        finally { try { await browser.DisposeAsync(); } finally { playwright.Dispose(); } }
    }
}
