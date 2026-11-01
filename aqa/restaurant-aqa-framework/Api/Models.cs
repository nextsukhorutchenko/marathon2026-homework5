namespace Restaurant.Aqa.Api;

public sealed record LoginResponse(string IdToken, string AccessToken);
public sealed record Principal(string Email, string Password, string Token, string Sub);
public sealed record GuestResponse(string SessionId, string SessionToken);
public sealed record OwnerSession(string GuestSessionId, string GroupReservationId, string Status);
public sealed record Dish(string Id, string Name, decimal Price);
public sealed record CartItem(Dish Dish, int Quantity);
public sealed record Participant(string Id, string Type, string Name, CartItem[] Items, decimal Total, bool IsSubmitted);
public sealed record Snapshot(Participant[] Participants, decimal Total);
public sealed record SubmittedItem(string DishId, int Quantity);
public sealed record Guest(string Id, string Token, string Name);
