using Restaurant.Aqa.Api;
using Restaurant.Aqa.Fixtures;

namespace Restaurant.Aqa.Tests;

[TestFixture, Category("Infrastructure")]
public sealed class InfrastructureTests
{
    private const string Prefix = "aqa-1234567890abcdef";

    [TestCase("aqa-1234567890abcdef-location")]
    [TestCase("owner-aqa-1234567890abcdef-group")]
    [TestCase("group-aqa-1234567890abcdef-group")]
    public void Cleanup_accepts_only_own_generated_keys(string key) =>
        Assert.DoesNotThrow(() => new FixtureOwnership(Prefix).EnsureOwned(key));

    [TestCase("real-customer")]
    [TestCase("aqa-another-run-reservation")]
    [TestCase("prefix-aqa-1234567890abcdef-location")]
    [TestCase("aqa-1234567890abcdefevil")]
    public void Cleanup_rejects_keys_from_outside_the_exact_run(string key) =>
        Assert.Throws<InvalidOperationException>(() => new FixtureOwnership(Prefix).EnsureOwned(key));

    [Test]
    public void Server_generated_guest_is_owned_only_by_actual_stored_group()
    {
        var guard = new FixtureOwnership(Prefix);
        Assert.DoesNotThrow(() => guard.EnsureOwned("server-generated-uuid", Prefix + "-group"));
        Assert.Throws<InvalidOperationException>(() => guard.EnsureOwned("server-generated-uuid", "real-group"));
    }

    [Test]
    public void Diagnostic_redacts_known_credentials_even_inside_free_text()
    {
        var redactor = new DiagnosticRedactor();
        redactor.Remember("Aqa!super-secret");
        Assert.That(redactor.Sanitize("Server echoed Aqa!super-secret"), Does.Not.Contain("Aqa!super-secret"));
    }

    [Test]
    public void Diagnostic_redacts_invitation_credentials_and_guest_token_hashes()
    {
        var value = "{\"reservationToken\":\"private-invite\",\"inviteToken\":\"private-invite\",\"token\":\"private-invite\",\"guestSessionTokenHash\":\"private-hash\"}";
        Assert.That(new DiagnosticRedactor().Sanitize(value), Does.Not.Contain("private-invite").And.Not.Contain("private-hash"));
    }

    [TestCase("{\"idToken\":\"secret-id-token\",\"sessionToken\":\"guest-secret\",\"password\":\"pass-secret\",\"message\":\"Order missing\"}")]
    [TestCase("Authorization: Bearer eyJheader.payload.signature")]
    public void Diagnostic_redacts_authentication_fields_and_bearer_tokens(string text)
    {
        var sanitized = new DiagnosticRedactor().Sanitize(text);
        Assert.That(sanitized, Does.Not.Contain("secret-id-token").And.Not.Contain("guest-secret")
            .And.Not.Contain("pass-secret").And.Not.Contain("eyJheader.payload.signature"));
    }

    [Test]
    public async Task Cleanup_runs_once_in_reverse_order_after_partial_setup()
    {
        var calls = new List<string>();
        var ledger = new CleanupLedger();
        ledger.Register("first", () => { calls.Add("first"); return Task.CompletedTask; });
        ledger.Register("second", () => { calls.Add("second"); return Task.CompletedTask; });
        ledger.Register("second", () => throw new Exception("Duplicate must not replace cleanup"));
        await ledger.RunAsync();
        await ledger.RunAsync();
        Assert.That(calls, Is.EqualTo(new[] { "second", "first" }));
    }

    [Test]
    public void Cleanup_attempts_remaining_resources_and_reports_failures()
    {
        bool firstCleaned = false;
        var ledger = new CleanupLedger();
        ledger.Register("first", () => { firstCleaned = true; return Task.CompletedTask; });
        ledger.Register("second", () => throw new IOException("Fixture service unavailable"));
        Assert.ThrowsAsync<AggregateException>(async () => await ledger.RunAsync());
        Assert.That(firstCleaned, Is.True);
    }
}
