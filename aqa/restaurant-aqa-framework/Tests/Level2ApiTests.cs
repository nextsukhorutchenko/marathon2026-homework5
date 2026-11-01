using Restaurant.Aqa.Api;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Api"), Category("Level2"), Parallelizable(ParallelScope.Self)]
public sealed class Level2ApiTests : LiveTestBase
{
    [Test]
    public async Task Owner_session_lookup_belongs_to_the_reservation()
    {
        await Fixture.PrepareAsync();
        var session = await Fixture.Api.OkAsync<OwnerSession>(HttpMethod.Get, $"/reservations/{Fixture.ReservationId}/guest-session-id", Fixture.Owner.Token);
        Assert.That(session.GuestSessionId, Is.EqualTo("owner-" + Fixture.GroupId));
        Assert.That(session.GroupReservationId, Is.EqualTo(Fixture.GroupId));
        Assert.That(session.Status, Is.EqualTo("Open"));
    }

    [Test]
    public async Task Submit_merges_submitted_items_excludes_draft_and_persists_both_statuses()
    {
        await Fixture.PrepareAsync();
        var first = await Fixture.JoinAsync("First Guest"); var second = await Fixture.JoinAsync("Second Guest"); var draft = await Fixture.JoinAsync("Draft Guest");
        await Fixture.CartAsync(first, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(first);
        await Fixture.CartAsync(second, Fixture.DishA, 3); await Fixture.CartAsync(second, Fixture.DishB, 1); await Fixture.SubmitGuestAsync(second);
        await Fixture.CartAsync(draft, Fixture.DishB, 9);
        await Fixture.WaitSnapshotAsync(value => value.Participants.Length == 4 && value.Total == 200);
        var reply = await Fixture.SubmitAsync(); Fixture.Api.Expect(reply, 200, "Submit consolidated order");
        var items = reply.Read<SubmittedItem[]>();
        Assert.That(items.ToDictionary(item => item.DishId, item => item.Quantity), Is.EquivalentTo(new Dictionary<string, int> { { Fixture.DishA, 5 }, { Fixture.DishB, 1 } }));
        await AssertPersistedOrder(items);
    }

    [Test]
    public async Task Owner_personal_contribution_is_included_once()
    {
        await Fixture.PrepareAsync();
        var personal = await Fixture.Api.SendAsync(HttpMethod.Put, $"/orders/{Fixture.ReservationId}", Fixture.Owner.Token, new { orderItems = new[] { new { dishId = Fixture.DishA, quantity = 1, state = 0 } } });
        Fixture.Api.Expect(personal, 200, "Owner personal cart");
        var guest = await Fixture.JoinAsync("Submitted Guest"); await Fixture.CartAsync(guest, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(value => value.Total == 30);
        var reply = await Fixture.SubmitAsync(); Fixture.Api.Expect(reply, 200, "Owner personal contribution submit");
        Assert.That(reply.Read<SubmittedItem[]>(), Is.EqualTo(new[] { new SubmittedItem(Fixture.DishA, 3) }));
        Assert.That((await Fixture.SnapshotAsync()).Total, Is.EqualTo(30));
    }

    [Test]
    public async Task Assigned_waiter_reads_the_exact_order_submitted_by_owner()
    {
        var waiter = await Fixture.AssignWaiterAsync(); await Fixture.PrepareAsync();
        var guest = await Fixture.JoinAsync("Submitted Guest"); await Fixture.CartAsync(guest, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(value => value.Total == 20);
        Fixture.Api.Expect(await Fixture.SubmitAsync(), 200, "Owner submit before waiter read");
        var order = await Fixture.Api.EventuallyAsync(() => Fixture.Api.SendAsync(HttpMethod.Get, $"/orders/{Fixture.ReservationId}", waiter.Token), reply => reply.Status == 200, "waiter order visibility");
        var node = System.Text.Json.Nodes.JsonNode.Parse(order.Body)!;
        // Product uses PascalCase for this existing endpoint.
        var rows = node["OrderItems"] ?? node["orderItems"];
        Assert.That(rows!.AsArray().Count, Is.EqualTo(1));
        var item = rows[0]!; var dish = item["Dish"] ?? item["dish"];
        Assert.That((dish!["Id"] ?? dish["id"])!.ToString(), Is.EqualTo(Fixture.DishA));
        Assert.That((item["Quantity"] ?? item["quantity"])!.GetValue<int>(), Is.EqualTo(2));
    }

    [Test]
    public async Task No_submitted_sessions_is_rejected_with_specific_message()
    {
        await Fixture.PrepareAsync();
        await AssertUnchanged(() => Fixture.SubmitAsync(), 400, "There are no submitted sessions.");
    }

    [Test]
    public async Task Submitted_cart_without_dishes_is_rejected()
    {
        await Fixture.PrepareAsync();
        await Fixture.SeedEdgeSessionAsync(Fixture.Prefix + "-empty-submitted", Fixture.GroupId, true, []);
        await Fixture.WaitSnapshotAsync(value => value.Participants.Length == 2);
        await AssertUnchanged(() => Fixture.SubmitAsync(), 400, "Submitted sessions contain no dishes.");
    }

    [Test]
    public async Task Missing_order_is_rejected_after_valid_submitted_cart()
    {
        var guest = await Fixture.JoinAsync("Submitted Guest"); await Fixture.CartAsync(guest, Fixture.DishA, 1); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(value => value.Total == 10);
        await AssertUnchanged(() => Fixture.SubmitAsync(), 400, "Order not found. Prepare the group pre-order first.");
    }

    [Test]
    public async Task Missing_reservation_is_rejected()
    {
        await PreparedSubmittedGuestAsync();
        var missing = Fixture.Prefix + "-missing-reservation"; var orphan = Fixture.Prefix + "-orphan-group";
        await Fixture.SeedGroupAsync(orphan, missing);
        await AssertUnchanged(() => Fixture.SubmitAsync(orphan, missing), 404, "Reservation not found.");
    }

    [Test]
    public async Task Missing_group_is_rejected()
    {
        await PreparedSubmittedGuestAsync();
        await AssertUnchanged(() => Fixture.SubmitAsync(Fixture.Prefix + "-missing-group"), 404, "Group reservation was not found");
    }

    [Test]
    public async Task Repeat_submit_returns_409_without_changing_the_consolidated_order()
    {
        await Fixture.PrepareAsync(); var guest = await Fixture.JoinAsync("Submitted Guest");
        await Fixture.CartAsync(guest, Fixture.DishA, 1); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(value => value.Total == 10);
        Fixture.Api.Expect(await Fixture.SubmitAsync(), 200, "First submit");
        var before = await Fixture.Store.GetAsync("Orders", new() { { "orderId", "group-" + Fixture.GroupId }, { "customerId", Fixture.Owner.Sub } });
        await AssertUnchanged(() => Fixture.SubmitAsync(), 409, "This group reservation is no longer open for changes.");
        var after = await Fixture.Store.GetAsync("Orders", new() { { "orderId", "group-" + Fixture.GroupId }, { "customerId", Fixture.Owner.Sub } });
        Assert.That(after["dishes"].L.Single().M["quantity"].N, Is.EqualTo(before["dishes"].L.Single().M["quantity"].N));
        Assert.That(after["status"].N, Is.EqualTo(before["status"].N));
    }

    [TestCase("Anonymous", 401)]
    [TestCase("OtherCustomer", 403)]
    [TestCase("Waiter", 403)]
    [TestCase("Guest", 401)]
    public async Task Only_owner_can_submit(string actor, int status)
    {
        await PreparedSubmittedGuestAsync();
        var token = await ActorToken(actor);
        await AssertUnchanged(() => Fixture.Api.SendAsync(HttpMethod.Post, $"/group-reservations/{Fixture.GroupId}/submit", token, new { reservationId = Fixture.ReservationId }), status, status == 401 ? "Unauthorized" : "Only the reservation owner can perform this action.");
    }

    private async Task AssertPersistedOrder(SubmittedItem[] expected)
    {
        var group = await Fixture.Store.GetAsync("Groups", new() { { "groupReservationId", Fixture.GroupId } });
        var order = await Fixture.Store.GetAsync("Orders", new() { { "orderId", "group-" + Fixture.GroupId }, { "customerId", Fixture.Owner.Sub } });
        Assert.That(group["status"].N, Is.EqualTo("1"), "Submitted group");
        Assert.That(order["status"].N, Is.EqualTo("3"), "SubmittedByCustomer order");
        var persisted = order["dishes"].L.ToDictionary(item => item.M["dishId"].S, item => int.Parse(item.M["quantity"].N));
        Assert.That(persisted, Is.EquivalentTo(expected.ToDictionary(item => item.DishId, item => item.Quantity)));
    }
}
