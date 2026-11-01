using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Restaurant.Aqa.Pages;

public sealed class WaiterPage(IPage page)
{
    public ILocator Dishes => page.Locator("app-dish-card.pre-order-dish");
    public async Task OpenPreOrderAsync()
    {
        // The fixture waiter owns exactly one reservation; use the real navigation action.
        var cards = page.Locator("app-card-reservation");
        await Expect(cards).ToHaveCountAsync(1);
        await cards.GetByRole(AriaRole.Button, new() { Name = "Pre-order", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/waiter-pre-order");
        await Expect(page.Locator("h2.title")).ToContainTextAsync("Pre-order of");
    }
    public async Task ExpectDishAsync(string name, int quantity)
    {
        var dish = Dishes.Filter(new() { HasText = name });
        await Expect(dish).ToHaveCountAsync(1);
        await Expect(dish.Locator(".pre-order-quantity")).ToHaveTextAsync("Quantity: " + quantity);
    }
}
