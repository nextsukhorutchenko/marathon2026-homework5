using Microsoft.Playwright;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Diagnostics;
using Restaurant.Aqa.Pages;
using static Microsoft.Playwright.Assertions;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Ui"), Parallelizable(ParallelScope.Self)]
public sealed class UiTests : LiveTestBase
{
    protected override bool RequireUi => true;

    private async Task WithBrowser(Func<IPage, Task> scenario, Principal? principal = null, bool waiter = false)
    {
        await using var ui = await UiSession.CreateAsync(Settings.UiBaseUrl!, Fixture.Redactor, Path.Combine(Fixture.Store.DirectoryPath, waiter ? "waiter" : "owner"));
        try
        {
            await ui.LoginAsync(principal ?? Fixture.Owner, waiter);
            await scenario(ui.Page);
        }
        catch (Exception error)
        {
            try { await ui.FailureArtifactsAsync(); }
            catch (Exception artifact) { TestContext.Error.WriteLine("UI artifact capture failed: " + artifact.GetType().Name); }
            throw new AssertionException(Fixture.Redactor.Sanitize(error.Message));
        }
    }

    [Test, Category("Level1")]
    public async Task Owner_UI_shows_submitted_draft_empty_carts_and_updates_quantities()
    {
        await Fixture.PrepareAsync();
        var submitted = await Fixture.JoinAsync("Submitted Guest"); var draft = await Fixture.JoinAsync("Draft Guest"); await Fixture.JoinAsync("Empty Guest");
        await Fixture.CartAsync(submitted, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(submitted);
        await Fixture.CartAsync(draft, Fixture.DishB, 1);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 4 && s.Total == 35);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId);
            await Expect(group.Participants).ToHaveCountAsync(4);
            await Expect(group.Participant(submitted.Name).Locator(".subtotal")).ToContainTextAsync("$20.00");
            await Expect(group.Participant(submitted.Name).Locator(".quantity")).ToHaveTextAsync("× 2");
            await Expect(group.Participant(submitted.Name).Locator(".chip")).ToHaveTextAsync("Submitted");
            await Expect(group.Participant(draft.Name).Locator(".subtotal")).ToContainTextAsync("$15.00");
            await Expect(group.Participant(draft.Name).Locator(".chip")).ToHaveTextAsync("Still choosing");
            await Expect(group.Participant("Empty Guest").Locator(".empty")).ToHaveTextAsync("No dishes yet");
            await Expect(group.Participant("Empty Guest").Locator(".subtotal")).ToContainTextAsync("$0.00");
            await Expect(group.Total).ToHaveTextAsync("$20.00");
            // A real guest API mutation changes the owner UI after its Refresh action.
            await Fixture.CartAsync(draft, Fixture.DishB, 2);
            await Fixture.WaitSnapshotAsync(s => s.Total == 65);
            await group.RefreshAsync();
            await Expect(group.Participant(draft.Name).Locator(".quantity")).ToHaveTextAsync("× 3");
            await Expect(group.Participant(draft.Name).Locator(".subtotal")).ToContainTextAsync("$45.00");
            await Expect(group.Total).ToHaveTextAsync("$20.00");
        });
    }

    [Test, Category("Level1")]
    public async Task Authenticated_owner_can_open_the_integrated_menu() => await WithBrowser(async page =>
    {
        await page.GotoAsync("/menu");
        await Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Menu", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Locator("app-dish-card").First).ToBeVisibleAsync();
    });

    [Test, Category("Level2")]
    public async Task Owner_UI_submits_merged_submitted_carts_and_locks_order()
    {
        await Fixture.PrepareAsync();
        var first = await Fixture.JoinAsync("First Guest"); var second = await Fixture.JoinAsync("Second Guest"); var draft = await Fixture.JoinAsync("Draft Guest");
        await Fixture.CartAsync(first, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(first);
        await Fixture.CartAsync(second, Fixture.DishA, 3); await Fixture.SubmitGuestAsync(second);
        await Fixture.CartAsync(draft, Fixture.DishB, 4);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 4 && s.Total == 110);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId);
            await Expect(group.Consolidated.GetByText("AQA Salad × 5", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(group.Consolidated).Not.ToContainTextAsync("AQA Soup");
            await Expect(group.Total).ToHaveTextAsync("$50.00");
            await group.SubmitAsync();
            await Expect(group.SubmitButton).ToHaveCountAsync(0);
            await Expect(page.GetByRole(AriaRole.Button, new() { Name = "Remove First Guest", Exact = true })).ToHaveCountAsync(0);
            await Expect(group.Total).ToHaveTextAsync("$50.00");
            var persisted = await Fixture.Store.GetAsync("Groups", new() { { "groupReservationId", Fixture.GroupId } });
            Assert.That(persisted["status"].N, Is.EqualTo("1"));
        });
    }

    [Test, Category("Level2")]
    public async Task Owner_UI_disables_submit_without_submitted_dishes()
    {
        await Fixture.PrepareAsync();
        var draft = await Fixture.JoinAsync("Draft Guest"); await Fixture.CartAsync(draft, Fixture.DishA, 1);
        await Fixture.WaitSnapshotAsync(s => s.Total == 10);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId);
            await Expect(group.SubmitButton).ToBeDisabledAsync(); await Expect(group.Total).ToHaveTextAsync("$0.00");
        });
    }

    [TestCase(false), TestCase(true), Category("Level3")]
    public async Task Owner_UI_removes_guest_and_recalculates_consolidated_total(bool submitted)
    {
        await Fixture.PrepareAsync();
        var removed = await Fixture.JoinAsync("Removed Guest"); var kept = await Fixture.JoinAsync("Kept Guest");
        await Fixture.CartAsync(removed, Fixture.DishA, 2); if (submitted) await Fixture.SubmitGuestAsync(removed);
        await Fixture.CartAsync(kept, Fixture.DishB, 1); await Fixture.SubmitGuestAsync(kept);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 3 && s.Total == 35);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId);
            await Expect(group.Total).ToHaveTextAsync(submitted ? "$35.00" : "$15.00");
            await group.RemoveAsync(removed.Name);
            await Expect(group.Total).ToHaveTextAsync("$15.00"); await Expect(group.Participants).ToHaveCountAsync(2);
            await Expect(group.Consolidated).Not.ToContainTextAsync("AQA Salad");
            Assert.That(await Fixture.Store.GetAsync("Sessions", new() { { "guestSessionId", removed.Id } }), Is.Empty);
        });
    }

    [Test, Category("Level3")]
    public async Task Owner_UI_removes_last_guest_and_returns_to_empty_order()
    {
        await Fixture.PrepareAsync(); var guest = await Fixture.JoinAsync("Last Guest");
        await Fixture.CartAsync(guest, Fixture.DishA, 1); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 2 && s.Total == 10);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId); await group.RemoveAsync(guest.Name);
            await Expect(group.Participants).ToHaveCountAsync(1); await Expect(group.Total).ToHaveTextAsync("$0.00");
            await Expect(group.SubmitButton).ToBeDisabledAsync();
            await Expect(group.Consolidated).ToContainTextAsync("No submitted dishes yet");
        });
    }

    [Test, Category("Level3"), Category("E2E")]
    public async Task Owner_removes_submits_and_waiter_sees_only_remaining_dishes()
    {
        var waiter = await Fixture.AssignWaiterAsync(); await Fixture.PrepareAsync();
        var removed = await Fixture.JoinAsync("Removed Guest"); var kept = await Fixture.JoinAsync("Kept Guest");
        await Fixture.CartAsync(removed, Fixture.DishB, 1); await Fixture.SubmitGuestAsync(removed);
        await Fixture.CartAsync(kept, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(kept);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 3 && s.Total == 35);
        await WithBrowser(async page =>
        {
            var group = new GroupOrderPage(page); await group.OpenAsync(Fixture.ReservationId);
            await group.RemoveAsync(removed.Name); await Expect(group.Total).ToHaveTextAsync("$20.00"); await group.SubmitAsync();
            // A different BrowserContext signs in as the assigned waiter through the UI.
            await WithBrowser(async waiterPage =>
            {
                var view = new WaiterPage(waiterPage); await view.OpenPreOrderAsync();
                await Expect(view.Dishes).ToHaveCountAsync(1); await view.ExpectDishAsync("AQA Salad", 2);
                await Expect(view.Dishes).Not.ToContainTextAsync("AQA Soup");
            }, waiter, true);
        });
    }
}
