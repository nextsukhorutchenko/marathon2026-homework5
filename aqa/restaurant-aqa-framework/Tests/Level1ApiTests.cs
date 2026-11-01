using Restaurant.Aqa.Api;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Api"), Category("Level1"), Parallelizable(ParallelScope.Self)]
public sealed class Level1ApiTests : LiveTestBase
{
    [Test]
    public async Task Owner_sees_all_participants_items_quantities_and_totals()
    {
        await Fixture.PrepareAsync();
        var submitted = await Fixture.JoinAsync("Submitted Guest");
        var draft = await Fixture.JoinAsync("Draft Guest");
        var empty = await Fixture.JoinAsync("Empty Guest");
        await Fixture.CartAsync(submitted, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(submitted);
        await Fixture.CartAsync(draft, Fixture.DishB, 1);
        var snapshot = await Fixture.WaitSnapshotAsync(value => value.Participants.Length == 4 && value.Total == 35);
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Participants.Single(person => person.Id == submitted.Id).IsSubmitted, Is.True);
            Assert.That(snapshot.Participants.Single(person => person.Id == draft.Id).IsSubmitted, Is.False);
            Assert.That(snapshot.Participants.Single(person => person.Id == empty.Id).Items, Is.Empty);
            Assert.That(snapshot.Participants.Single(person => person.Id == empty.Id).Total, Is.Zero);
            Assert.That(snapshot.Participants.Single(person => person.Id == submitted.Id).Items.Single().Quantity, Is.EqualTo(2));
            Assert.That(snapshot.Participants.Single(person => person.Id == submitted.Id).Items.Single().Dish.Name, Is.EqualTo("AQA Salad"));
            Assert.That(snapshot.Participants.Single(person => person.Id == draft.Id).Items.Single().Dish.Name, Is.EqualTo("AQA Soup"));
            Assert.That(snapshot.Total, Is.EqualTo(snapshot.Participants.Sum(person => person.Total)));
            foreach (var person in snapshot.Participants)
                Assert.That(person.Total, Is.EqualTo(person.Items.Sum(item => item.Quantity * item.Dish.Price)));
        });
    }

    [Test]
    public async Task Total_changes_when_guest_adds_more_of_the_same_dish()
    {
        var guest = await Fixture.JoinAsync("Changing Guest");
        await Fixture.CartAsync(guest, Fixture.DishA, 1);
        await Fixture.WaitSnapshotAsync(value => value.Total == 10);
        await Fixture.CartAsync(guest, Fixture.DishA, 2);
        var snapshot = await Fixture.WaitSnapshotAsync(value => value.Total == 30);
        Assert.That(snapshot.Participants.Single().Items.Single().Quantity, Is.EqualTo(3));
    }

    [Test]
    public async Task Raw_empty_group_returns_empty_participants_and_zero_total()
    {
        var snapshot = await Fixture.SnapshotAsync();
        Assert.That(snapshot.Participants, Is.Empty);
        Assert.That(snapshot.Total, Is.Zero);
    }

    [TestCase("Anonymous", 401)]
    [TestCase("Guest", 401)]
    [TestCase("OtherCustomer", 403)]
    [TestCase("Waiter", 403)]
    public async Task Only_owner_can_read_group_sessions(string actor, int status)
    {
        await PreparedSubmittedGuestAsync();
        var token = await ActorToken(actor);
        await AssertUnchanged(() => Fixture.Api.SendAsync(HttpMethod.Get, $"/guest/groups/{Fixture.GroupId}/sessions", token), status, status == 401 ? "Unauthorized" : "Only the reservation owner can perform this action.");
    }

    [Test]
    public async Task Missing_group_returns_404()
    {
        await PreparedSubmittedGuestAsync();
        await AssertUnchanged(() => Fixture.Api.SendAsync(HttpMethod.Get, $"/guest/groups/{Fixture.Prefix}-missing/sessions", Fixture.Owner.Token), 404, "Group reservation was not found");
    }
}
