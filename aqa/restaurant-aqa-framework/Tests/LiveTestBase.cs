using Restaurant.Aqa.Api;
using Restaurant.Aqa.Configuration;
using Restaurant.Aqa.Fixtures;

namespace Restaurant.Aqa.Tests;

public abstract class LiveTestBase
{
    protected ScenarioFixture Fixture = null!;
    protected virtual bool RequireUi => false;
    protected TestSettings Settings = null!;

    [SetUp]
    public async Task SetUpLiveFixture()
    {
        Settings = TestSettings.Load(RequireUi);
        Fixture = await ScenarioFixture.CreateAsync(Settings);
    }

    [TearDown]
    public async Task CleanUpLiveFixture()
    {
        if (Fixture is not null) await Fixture.DisposeAsync();
    }

    protected void Error(ApiReply reply, int status, string message)
    {
        Fixture.Api.Expect(reply, status, "Negative contract");
        Assert.That(Fixture.Redactor.Sanitize(reply.Error), Is.EqualTo(message));
    }

    protected async Task<string?> ActorToken(string actor) => actor switch
    {
        "Anonymous" => null,
        "Guest" => await GuestActorToken(),
        "Waiter" => (await Fixture.PrincipalAsync("waiter")).Token,
        "OtherCustomer" => (await Fixture.PrincipalAsync("other")).Token,
        _ => throw new InvalidOperationException("Unknown actor")
    };

    private async Task<string> GuestActorToken()
    {
        var guest = await Fixture.JoinAsync("Unauthorized guest");
        await Fixture.WaitSnapshotAsync(snapshot => snapshot.Participants.Any(p => p.Id == guest.Id));
        return guest.Token;
    }

    protected async Task AssertUnchanged(Func<Task<ApiReply>> action, int status, string message)
    {
        var before = await Fixture.SnapshotAsync();
        await PersistedState.VerifyUnchangedAsync(Fixture.Store.CaptureStateAsync, action, reply => Error(reply, status, message));
        var after = await Fixture.SnapshotAsync();
        Assert.That(System.Text.Json.JsonSerializer.Serialize(after), Is.EqualTo(System.Text.Json.JsonSerializer.Serialize(before)));
    }

    protected async Task<Guest> PreparedSubmittedGuestAsync(string name = "Protected Guest")
    {
        await Fixture.PrepareAsync();
        var guest = await Fixture.JoinAsync(name);
        await Fixture.CartAsync(guest, Fixture.DishA, 2); await Fixture.SubmitGuestAsync(guest);
        await Fixture.WaitSnapshotAsync(s => s.Participants.Length == 2 && s.Total == 20);
        return guest;
    }
}
