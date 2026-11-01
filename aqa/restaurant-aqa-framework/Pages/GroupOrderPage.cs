using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Restaurant.Aqa.Pages;

public sealed class GroupOrderPage(IPage page)
{
    public ILocator Participants => page.Locator("app-participant-card article");
    public ILocator Participant(string name) => Participants.Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = "Guest: " + name, Exact = true }) });
    public ILocator Total => page.Locator(".grand-total strong");
    public ILocator Consolidated => page.GetByRole(AriaRole.Region, new() { Name = "Consolidated pre-order", Exact = true });
    public ILocator SubmitButton => page.GetByRole(AriaRole.Button, new() { Name = "Submit Pre-Order", Exact = true });

    public async Task OpenAsync(string reservationId)
    {
        await page.GotoAsync($"/reservations/{reservationId}/group-order");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Group Pre-Order", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("main.page")).ToHaveAttributeAsync("aria-busy", "false");
        await Expect(page.GetByRole(AriaRole.Alert)).ToHaveCountAsync(0);
    }
    public Task RefreshAsync() => page.GetByRole(AriaRole.Button, new() { Name = "Refresh", Exact = true }).ClickAsync();
    public async Task RemoveAsync(string name)
    {
        await Participant(name).GetByRole(AriaRole.Button, new() { Name = "Remove " + name, Exact = true }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Remove Guest", Exact = true });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Remove Guest", Exact = true }).ClickAsync();
        await Expect(dialog).ToHaveCountAsync(0);
        await Expect(Participant(name)).ToHaveCountAsync(0);
    }
    public async Task SubmitAsync()
    {
        await SubmitButton.ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Submit Pre-Order", Exact = true });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Submit", Exact = true }).ClickAsync();
        await Expect(page.GetByText("Sent to Restaurant", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(page.GetByText("Your pre-order has been sent to the restaurant.", new() { Exact = true })).ToBeVisibleAsync();
    }
}
