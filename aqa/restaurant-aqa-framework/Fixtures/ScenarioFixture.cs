using System.Security.Cryptography;
using System.Text.Json;
using Restaurant.Aqa.Api;
using Restaurant.Aqa.Configuration;

namespace Restaurant.Aqa.Fixtures;

public sealed class ScenarioFixture : IAsyncDisposable
{
    public AwsFixtureStore Store { get; }
    public RestaurantApi Api { get; }
    public DiagnosticRedactor Redactor { get; } = new();
    public Principal Owner { get; private set; } = null!;
    public string Prefix => Store.Prefix;
    public string ReservationId => Prefix + "-reservation";
    public string GroupId => Prefix + "-group";
    public string DishA => Prefix + "-salad";
    public string DishB => Prefix + "-soup";
    public string LocationId => Prefix + "-location";
    public string TableId => Prefix + "-table";
    public DateTime Start { get; } = DateTime.UtcNow.Date.AddDays(2).AddHours(14);
    private readonly Dictionary<string, Principal> principals = new();
    private ScenarioFixture(TestSettings settings) { Store = new(settings); Api = new(settings.ApiBaseUrl, Redactor); }

    public static async Task<ScenarioFixture> CreateAsync(TestSettings settings)
    {
        var fixture = new ScenarioFixture(settings);
        try
        {
            await fixture.Store.InitializeAsync();
            fixture.Owner = await fixture.PrincipalAsync("owner");
            await fixture.Store.PutAsync("Locations", new() { ["id"] = fixture.LocationId, ["address"] = "AQA isolated location", ["description"] = "Disposable automation fixture", ["totalCapacity"] = 8, ["averageOccupancy"] = "0", ["rating"] = 0, ["specialtyDishIds"] = Array.Empty<string>() });
            await fixture.Store.PutAsync("Tables", new() { ["id"] = fixture.TableId, ["locationId"] = fixture.LocationId, ["tableNumber"] = 99, ["capacity"] = 8 });
            await fixture.SeedDishAsync(fixture.DishA, "AQA Salad", 10);
            await fixture.SeedDishAsync(fixture.DishB, "AQA Soup", 15);
            await fixture.SeedReservationAsync(fixture.ReservationId);
            await fixture.SeedGroupAsync(fixture.GroupId, fixture.ReservationId);
            await fixture.Store.TrackGeneratedAsync("Sessions", new() { { "guestSessionId", "owner-" + fixture.GroupId } });
            await fixture.Store.TrackGeneratedAsync("Orders", new() { { "orderId", "group-" + fixture.GroupId }, { "customerId", fixture.Owner.Sub } });
            return fixture;
        }
        catch (Exception setup)
        {
            try { await fixture.DisposeAsync(); }
            catch (Exception cleanup) { throw new AggregateException("Fixture setup and cleanup failed.", setup, cleanup); }
            throw;
        }
    }

    public async Task<Principal> PrincipalAsync(string role)
    {
        if (principals.TryGetValue(role, out var existing)) return existing;
        var email = Prefix + "-" + role + "@example.com";
        var password = "Aqa!Aa7" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        Redactor.Remember(password);
        await Store.ReserveIdentityAsync(email);
        var reply = await Api.SendAsync(HttpMethod.Post, "/register", body: new { firstName = "Automation", lastName = "Tester", email, password, confirmPassword = password });
        Api.Expect(reply, 200, "Register disposable " + role);
        if (role == "waiter") await Store.MakeWaiterAsync(email);
        var login = await Api.OkAsync<LoginResponse>(HttpMethod.Post, "/signin", body: new { email, password });
        Redactor.Remember(login.IdToken); Redactor.Remember(login.AccessToken);
        var payload = login.IdToken.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var sub = JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement.GetProperty("sub").GetString()!;
        var principal = new Principal(email, password, login.IdToken, sub);
        principals.Add(role, principal);
        return principal;
    }

    public async Task<Principal> AssignWaiterAsync()
    {
        var waiter = await PrincipalAsync("waiter");
        await Store.PutAsync("Waiters", new() { ["userId"] = waiter.Sub, ["locationId"] = LocationId, ["createdAt"] = DateTime.UtcNow.ToString("O"), ["reservations"] = new[] { ReservationId } });
        // The assigned waiter is part of fixture preparation, not a product mutation under test.
        await ReplaceReservationAsync(waiter.Sub);
        return waiter;
    }

    private async Task ReplaceReservationAsync(string waiterId)
    {
        await Store.DeleteOwnedSeedAsync("Reservations", new() { { "id", ReservationId } });
        await SeedReservationAsync(ReservationId, waiterId);
    }

    public Task SeedDishAsync(string id, string name, decimal price) => Store.PutAsync("Dishes", new() { ["id"] = id, ["name"] = name, ["description"] = "Disposable AQA dish", ["dishType"] = 0, ["price"] = price, ["state"] = 1, ["locationId"] = LocationId, ["weight"] = "200 g", ["calories"] = "100", ["proteins"] = "1", ["fats"] = "1", ["carbohydrates"] = "1", ["vitamins"] = "A" });
    public Task SeedReservationAsync(string id, string waiterId = "fixture-unassigned") => Store.PutAsync("Reservations", new() { ["id"] = id, ["tableId"] = TableId, ["customerId"] = Owner.Sub, ["waiterId"] = waiterId, ["createdByWaiter"] = false, ["startTime"] = Start.ToString("yyyy-MM-ddTHH:mm:ss.000Z"), ["endTime"] = Start.AddHours(2).ToString("yyyy-MM-ddTHH:mm:ss.000Z"), ["guestCount"] = 8, ["status"] = 0 });
    public Task SeedGroupAsync(string id, string reservationId)
    {
        var invitation = Guid.NewGuid().ToString("N");
        Redactor.Remember(invitation);
        return Store.PutAsync("Groups", new() { ["groupReservationId"] = id, ["reservationId"] = reservationId, ["reservationToken"] = invitation, ["status"] = 0 });
    }
    public Task SeedEdgeSessionAsync(string id, string groupId, bool submitted, object[] dishes) => Store.PutAsync("Sessions", new() { ["guestSessionId"] = id, ["groupReservationId"] = groupId, ["guestName"] = "Edge fixture", ["isOwnerSession"] = false, ["isSubmitted"] = submitted, ["dishes"] = dishes });

    public async Task PrepareAsync()
    {
        await Store.TrackGeneratedAsync("Sessions", new() { { "guestSessionId", "owner-" + GroupId } });
        await Store.TrackGeneratedAsync("Orders", new() { { "orderId", "group-" + GroupId }, { "customerId", Owner.Sub } });
        await Api.EventuallyAsync(() => Api.SendAsync(HttpMethod.Get, $"/reservations/{ReservationId}/invite-token", Owner.Token), reply => reply.Status == 200, "reservation/group index visibility");
        await Api.OkAsync<OwnerSession>(HttpMethod.Post, $"/reservations/{ReservationId}/group-pre-order", Owner.Token, new { });
        await Api.EventuallyAsync(() => Api.SendAsync(HttpMethod.Get, $"/orders/{ReservationId}", Owner.Token), reply => FixtureReadiness.OrderReady(reply, "group-" + GroupId, ReservationId), "exact prepared order index visibility before editing");
    }

    public async Task<Guest> JoinAsync(string name, string? groupId = null)
    {
        groupId ??= GroupId;
        var attempt = await Store.BeginGuestJoinAsync(groupId);
        var result = await Api.OkAsync<GuestResponse>(HttpMethod.Post, "/guest/sessions", body: new { groupReservationId = groupId, guestName = name });
        Redactor.Remember(result.SessionToken);
        await Store.TrackRelatedAsync("Sessions", new() { { "guestSessionId", result.SessionId } }, "groupReservationId", groupId);
        await Store.CompleteGuestJoinAsync(attempt);
        var guest = new Guest(result.SessionId, result.SessionToken, name);
        await Api.EventuallyAsync(() => Api.SendAsync(HttpMethod.Get, $"/guest/sessions/{guest.Id}/menu", guest.Token), reply => FixtureReadiness.MenuReady(reply, DishA, DishB), "guest token and both seeded dishes visibility");
        return guest;
    }

    public async Task CartAsync(Guest guest, string dishId, int quantity)
    {
        var reply = await Api.SendAsync(HttpMethod.Post, $"/guest/sessions/{guest.Id}/dishes", guest.Token, new { dishId, quantity });
        Api.Expect(reply, 200, "Add guest dish");
    }
    public async Task SubmitGuestAsync(Guest guest)
    {
        var reply = await Api.SendAsync(HttpMethod.Post, $"/guest/sessions/{guest.Id}/submit", guest.Token, new { });
        Api.Expect(reply, 200, "Submit guest cart");
    }
    public Task<Snapshot> SnapshotAsync() => Api.OkAsync<Snapshot>(HttpMethod.Get, $"/guest/groups/{GroupId}/sessions", Owner.Token);
    public Task<Snapshot> WaitSnapshotAsync(Func<Snapshot, bool> ready) => Api.EventuallyAsync(SnapshotAsync, ready, "expected participant snapshot");
    public Task<ApiReply> SubmitAsync(string? group = null, string? reservation = null, string? token = null) => Api.SendAsync(HttpMethod.Post, $"/group-reservations/{group ?? GroupId}/submit", token ?? Owner.Token, new { reservationId = reservation ?? ReservationId });
    public Task<ApiReply> RemoveAsync(string guestId, string? token = null) => Api.SendAsync(HttpMethod.Delete, $"/guest/groups/{GroupId}/sessions/{guestId}", token ?? Owner.Token);
    public async ValueTask DisposeAsync() { try { await Store.DisposeAsync(); } finally { Api.Dispose(); } }
}
