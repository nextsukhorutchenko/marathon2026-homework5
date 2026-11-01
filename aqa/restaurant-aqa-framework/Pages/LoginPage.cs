using Microsoft.Playwright;
using Restaurant.Aqa.Api;
using static Microsoft.Playwright.Assertions;

namespace Restaurant.Aqa.Pages;

public sealed class LoginPage(IPage page, DiagnosticRedactor redactor)
{
    public async Task SignInAsync(Principal principal, bool waiter = false)
    {
        await page.GotoAsync("/login");
        var form = page.Locator("app-login-form");
        await form.GetByPlaceholder("e.g. username@domain.com", new() { Exact = true }).FillAsync(principal.Email);
        await form.Locator("input[formcontrolname='password']").FillAsync(principal.Password);
        var response = await page.RunAndWaitForResponseAsync(
            () => form.GetByRole(AriaRole.Button, new() { Name = "Sign In", Exact = true }).ClickAsync(),
            response => new Uri(response.Url).AbsolutePath.EndsWith("/signin", StringComparison.Ordinal) && response.Request.Method == "POST");
        if (response.Status != 200) throw new AssertionException("UI sign-in returned HTTP " + response.Status);
        var json = await response.JsonAsync();
        if (json.HasValue)
            foreach (var key in new[] { "idToken", "accessToken" })
                if (json.Value.TryGetProperty(key, out var token)) redactor.Remember(token.GetString() ?? "");
        // The route changes only after /users/me has produced the authenticated role.
        await page.WaitForURLAsync(waiter ? "**/waiter-reservations" : "**/main");
        await Expect(form).ToHaveCountAsync(0);
    }
}
