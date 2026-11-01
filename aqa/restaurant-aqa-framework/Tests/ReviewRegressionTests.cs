using System.Text.Json;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Fixtures;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Infrastructure")]
public sealed class ReviewRegressionTests
{
    [Test]
    public void Unknown_join_stays_in_recovery_journal_and_cannot_claim_verified_cleanup()
    {
        var joins = new PendingGuestJoins(); var attempt = joins.Begin("aqa-test-group");
        Assert.That(joins.Attempts[attempt], Is.EqualTo("aqa-test-group"));
        Assert.Throws<InvalidOperationException>(() => joins.RequireVerified("aqa-test-group"));
        joins.Complete(attempt);
        Assert.DoesNotThrow(() => joins.RequireVerified("aqa-test-group"));
        Assert.That(joins.Attempts, Is.Empty);
    }

    [Test]
    public void Menu_readiness_requires_both_seeded_dishes()
    {
        Assert.That(FixtureReadiness.MenuReady(new(200, "{\"dishes\":[\"salad\"]}"), "salad", "soup"), Is.False);
        Assert.That(FixtureReadiness.MenuReady(new(200, "{\"dishes\":[\"salad\",\"soup\"]}"), "salad", "soup"), Is.True);
        Assert.That(FixtureReadiness.MenuReady(new(500, "salad soup"), "salad", "soup"), Is.False);
    }

    [TestCase("group-status"), TestCase("order-status"), TestCase("reservation-status")]
    public void Rejected_action_must_not_change_persisted_state_omitted_from_participant_DTO(string resource)
    {
        string state = "before";
        Task<IReadOnlyDictionary<string, string>> Capture() => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string> { { resource, state } });
        Assert.ThrowsAsync<AssertionException>(() => PersistedState.VerifyUnchangedAsync(Capture, () => { state = "changed"; return Task.FromResult(new ApiReply(400, "{}")); }, _ => { }));
    }

    [Test]
    public void Order_readiness_requires_the_exact_fixture_order_before_editing()
    {
        Assert.That(FixtureReadiness.OrderReady(new(404, "{}"), "order", "reservation"), Is.False);
        Assert.That(FixtureReadiness.OrderReady(new(200, "{\"orderId\":\"other\",\"reservationId\":\"reservation\"}"), "order", "reservation"), Is.False);
        Assert.That(FixtureReadiness.OrderReady(new(200, "{\"orderId\":\"order\",\"reservationId\":\"reservation\"}"), "order", "reservation"), Is.True);
    }

    [Test]
    public void State_fingerprint_ignores_map_order_detects_status_change_and_does_not_expose_values()
    {
        var first = JsonDocument.Parse("{\"status\":0,\"nested\":{\"b\":2,\"a\":1},\"sessionTokenHash\":\"private-value\"}").RootElement;
        var reordered = JsonDocument.Parse("{\"sessionTokenHash\":\"private-value\",\"nested\":{\"a\":1,\"b\":2},\"status\":0}").RootElement;
        var changed = JsonDocument.Parse("{\"status\":1}").RootElement;
        Assert.That(PersistedState.Fingerprint(first), Is.EqualTo(PersistedState.Fingerprint(reordered)));
        Assert.That(PersistedState.Fingerprint(first), Is.Not.EqualTo(PersistedState.Fingerprint(changed)).And.Not.Contain("private-value"));
    }
}
