using Restaurant.Aqa.Api;
namespace Restaurant.Aqa.Fixtures;

public static class FixtureReadiness
{
    public static bool MenuReady(ApiReply reply, params string[] dishes) => reply.Status == 200 && dishes.All(id => reply.Body.Contains(System.Text.Json.JsonSerializer.Serialize(id), StringComparison.Ordinal));
    public static bool OrderReady(ApiReply reply, string orderId, string reservationId)
    {
        if (reply.Status != 200) return false;
        var value = reply.Read<OrderReference>();
        return value.OrderId == orderId && value.ReservationId == reservationId;
    }
    private sealed record OrderReference(string OrderId, string ReservationId);
}
