namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Api"), Category("Level3"), Parallelizable(ParallelScope.Self)]
public sealed class Level3ApiTests : LiveTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task Removing_draft_or_submitted_guest_recalculates_remaining_total(bool submitted)
    {
        var removed = await Fixture.JoinAsync("Removed Guest"); var remaining = await Fixture.JoinAsync("Remaining Guest");
        await Fixture.CartAsync(removed, Fixture.DishA, 2); await Fixture.CartAsync(remaining, Fixture.DishB, 1);
        if (submitted) await Fixture.SubmitGuestAsync(removed);
        await Fixture.WaitSnapshotAsync(value => value.Total == 35 && value.Participants.Length == 2);
        var reply = await Fixture.RemoveAsync(removed.Id); Fixture.Api.Expect(reply, 200, "Delete guest");
        var snapshot = reply.Read<Restaurant.Aqa.Api.Snapshot>();
        Assert.That(snapshot.Participants.Select(person => person.Id), Is.EqualTo(new[] { remaining.Id }));
        Assert.That(snapshot.Total, Is.EqualTo(15));
        Assert.That(await Fixture.Store.GetAsync("Sessions", new() { { "guestSessionId", removed.Id } }), Is.Empty);
    }

    [Test]
    public async Task Removing_last_guest_returns_empty_list_and_zero_total()
    {
        var guest = await Fixture.JoinAsync("Last Guest"); await Fixture.CartAsync(guest, Fixture.DishA, 1);
        await Fixture.WaitSnapshotAsync(value => value.Participants.Length == 1);
        var reply = await Fixture.RemoveAsync(guest.Id); Fixture.Api.Expect(reply, 200, "Delete last guest");
        var snapshot = reply.Read<Restaurant.Aqa.Api.Snapshot>();
        Assert.That(snapshot.Participants, Is.Empty); Assert.That(snapshot.Total, Is.Zero);
    }

    [Test]
    public async Task Foreign_group_guest_is_400_and_neither_group_changes()
    {
        var secondReservation = Fixture.Prefix + "-second-reservation"; var secondGroup = Fixture.Prefix + "-second-group";
        await Fixture.SeedReservationAsync(secondReservation); await Fixture.SeedGroupAsync(secondGroup, secondReservation);
        var foreign = await Fixture.JoinAsync("Foreign Guest", secondGroup);
        await Fixture.Api.EventuallyAsync(() => Fixture.Api.OkAsync<Restaurant.Aqa.Api.Snapshot>(HttpMethod.Get, $"/guest/groups/{secondGroup}/sessions", Fixture.Owner.Token), value => value.Participants.Length == 1, "foreign group membership");
        var before = await Fixture.Store.GetAsync("Sessions", new() { { "guestSessionId", foreign.Id } });
        await AssertUnchanged(() => Fixture.RemoveAsync(foreign.Id), 400, "Guest session does not belong to this group reservation.");
        var after = await Fixture.Store.GetAsync("Sessions", new() { { "guestSessionId", foreign.Id } });
        Assert.That(after["groupReservationId"].S, Is.EqualTo(before["groupReservationId"].S));
        var foreignSnapshot = await Fixture.Api.OkAsync<Restaurant.Aqa.Api.Snapshot>(HttpMethod.Get, $"/guest/groups/{secondGroup}/sessions", Fixture.Owner.Token);
        Assert.That(foreignSnapshot.Participants.Single().Id, Is.EqualTo(foreign.Id));
    }

    [Test]
    public async Task Missing_guest_is_404()
    {
        await PreparedSubmittedGuestAsync();
        await AssertUnchanged(() => Fixture.RemoveAsync(Fixture.Prefix + "-missing-guest"), 404, "Guest session not found.");
    }

    [Test]
    public async Task Owner_session_cannot_be_removed()
    {
        await PreparedSubmittedGuestAsync();
        await AssertUnchanged(() => Fixture.RemoveAsync("owner-" + Fixture.GroupId), 400, "The owner session cannot be removed.");
    }

    [TestCase("Anonymous", 401)]
    [TestCase("OtherCustomer", 403)]
    [TestCase("Waiter", 403)]
    [TestCase("Guest", 401)]
    public async Task Only_owner_can_remove_a_guest(string actor, int status)
    {
        var guest = await PreparedSubmittedGuestAsync(); var token = await ActorToken(actor);
        await Fixture.WaitSnapshotAsync(value => value.Participants.Any(person => person.Id == guest.Id));
        await AssertUnchanged(() => Fixture.Api.SendAsync(HttpMethod.Delete, $"/guest/groups/{Fixture.GroupId}/sessions/{guest.Id}", token), status, status == 401 ? "Unauthorized" : "Only the reservation owner can perform this action.");
    }
}
