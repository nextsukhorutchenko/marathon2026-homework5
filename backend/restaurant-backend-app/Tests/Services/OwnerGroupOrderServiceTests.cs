using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using Moq;

namespace APIHandler.Tests.Services;

public class OwnerGroupOrderServiceTests
{
    private readonly Mock<IGroupReservationRepository> groups = new();
    private readonly Mock<IReservationRepository> reservations = new();
    private readonly Mock<IGuestSessionRepository> sessions = new();
    private readonly Mock<IDishRepository> dishes = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IGroupOrderTransactionRepository> transactions = new();
    private readonly Mock<ILocationService> locations = new();
    private readonly GroupReservation group = new() { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "invite", Status = GroupReservationStatus.Open };
    private readonly Reservation reservation = new() { Id = "reservation", CustomerId = "owner", TableId = "table", WaiterId = "waiter", Status = ReservationStatus.Reserved, StartTime = DateTime.UtcNow.AddDays(1) };
    private readonly Order order = new() { OrderId = "order", ReservationId = "reservation", CustomerId = "owner", ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = [], Status = OrderStatus.Created };
    private readonly OwnerGroupOrderService service;

    public OwnerGroupOrderServiceTests()
    {
        groups.Setup(repo => repo.GetGroupReservationByIdAsync("group")).ReturnsAsync(group);
        groups.Setup(repo => repo.GetByReservationIdAsync("reservation")).ReturnsAsync(group);
        reservations.Setup(repo => repo.GetReservationByIdAsync("reservation")).ReturnsAsync(reservation);
        orders.Setup(repo => repo.GetOrderByReservationIdAsync("reservation")).ReturnsAsync(order);
        orders.Setup(repo => repo.GetOrderByIdAsync("order", "owner")).ReturnsAsync(order);
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([]);
        sessions.Setup(repo => repo.GetByGuestSessionId(It.IsAny<string>())).Returns(async (string identifier) =>
            (await sessions.Object.GetAllByGroupReservationIdAsync("group")).FirstOrDefault(session => session.Id == identifier));
        dishes.Setup(repo => repo.GetDishesByIdsAsync(It.IsAny<string[]>())).ReturnsAsync(new[] {
            new Dish { Id = "salad", Name = "Salad", Price = 180, State = APIHandler.Constants.Dishes.DishState.Available }, new Dish { Id = "soup", Name = "Soup", Price = 220, State = APIHandler.Constants.Dishes.DishState.Available }
        });
        locations.Setup(value => value.GetLocationByTableIdAsync("table")).ReturnsAsync(new Location { Id = "location", Address = "Address", Description = "Restaurant", AverageOccupancy = "Low" });
        service = new OwnerGroupOrderService(groups.Object, reservations.Object, sessions.Object, dishes.Object, orders.Object, transactions.Object, locations.Object);
    }

    private static GuestSession Session(string identifier, bool submitted, params OrderItem[] items) => new() {
        Id = identifier, GroupReservationId = "group", GuestName = identifier, IsSubmitted = submitted, Dishes = items.ToList()
    };
    private static OrderItem Item(string identifier, int quantity) => new() { DishId = identifier, Quantity = quantity };

    [Fact]
    public async Task PreparationCreatesOnlyMissingOwnerSessionAndEmptyOrder()
    {
        orders.Setup(repo => repo.GetOrderByReservationIdAsync("reservation")).ReturnsAsync((Order?)null);
        var result = await service.PrepareAsync("owner", "reservation");
        Assert.Equal("owner-group", result.GuestSessionId);
        transactions.Verify(repo => repo.PrepareAsync(It.Is<Order>(value => value.CustomerId == "owner" && value.Dishes.Count == 0), It.Is<GuestSession>(value => value.OwnerUserId == "owner" && value.IsOwnerSession), group), Times.Once);
    }

    [Fact]
    public async Task PreparationNeverReplacesExistingPersonalOrder()
    {
        await service.PrepareAsync("owner", "reservation");
        transactions.Verify(repo => repo.PrepareAsync(null, It.IsAny<GuestSession>(), group), Times.Once);
    }

    [Fact]
    public async Task SnapshotPreservesOwnerContributionAfterConsolidationWithoutDoubleCounting()
    {
        group.Status = GroupReservationStatus.Submitted;
        order.Dishes = [Item("salad", 3)];
        order.GroupOwnerDishes = [Item("salad", 1)];
        var ownerSession = Session("owner-session", false);
        ownerSession.IsOwnerSession = true;
        ownerSession.OwnerUserId = "owner";
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([ownerSession, Session("guest", true, Item("salad", 2))]);
        var result = await service.GetSessionsAsync("owner", "group");
        Assert.Equal(540m, result.Total);
        Assert.Equal(180m, result.Participants.First().Total);
    }

    [Fact]
    public async Task SubmissionRejectsReservationsPastExistingPreOrderCutoff()
    {
        reservation.StartTime = DateTime.UtcNow.AddMinutes(10);
        await Assert.ThrowsAsync<GroupOrderConflictException>(() => service.SubmitAsync("owner", "group", "reservation"));
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Submit_PreservesExistingOwnerDishesWhenAddingGuestContributions()
    {
        order.Dishes = [Item("salad", 1)];
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("guest", true, Item("salad", 2))]);
        var result = await service.SubmitAsync("owner", "group", "reservation");
        Assert.Equal(3, Assert.Single(result).Quantity);
    }

    [Fact]
    public async Task Submit_DoesNotIncludeDeletedSessionStillInIndex()
    {
        var indexed = Session("deleted", true, Item("salad", 1));
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([indexed]);
        sessions.Setup(repo => repo.GetByGuestSessionId("deleted")).ReturnsAsync((GuestSession?)null);
        await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "reservation"));
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Snapshot_UsesServerPricesAndIdentifiesOwner()
    {
        var ownerSession = Session("owner-session", false, Item("salad", 2));
        ownerSession.IsOwnerSession = true;
        ownerSession.OwnerUserId = "owner";
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([ownerSession, Session("guest", true, Item("soup", 1))]);
        var result = await service.GetSessionsAsync("owner", "group");
        Assert.Equal(580m, result.Total);
        Assert.Equal("Owner", result.Participants.First().Type.ToString());
        Assert.Equal(360m, result.Participants.First().Total);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("lookup")]
    [InlineData("submit")]
    [InlineData("delete")]
    [InlineData("prepare")]
    public async Task EveryOwnerAction_RejectsAnotherUser(string action)
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(async () => {
            switch (action) {
                case "read": await service.GetSessionsAsync("intruder", "group"); break;
                case "lookup": await service.GetOwnerSessionAsync("intruder", "reservation"); break;
                case "submit": await service.SubmitAsync("intruder", "group", "reservation"); break;
                case "delete": await service.DeleteSessionAsync("intruder", "group", "guest"); break;
                case "prepare": await service.PrepareAsync("intruder", "reservation"); break;
            }
        });
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Lookup_SelectsOnlyOwnersOwnSessionWithoutWriting()
    {
        var ownerSession = Session("owner-session", false);
        ownerSession.IsOwnerSession = true;
        ownerSession.OwnerUserId = "owner";
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("guest", false), ownerSession]);
        var result = await service.GetOwnerSessionAsync("owner", "reservation");
        Assert.Equal("owner-session", result.GuestSessionId);
        Assert.Equal("group", result.GroupReservationId);
        sessions.Verify(repo => repo.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task Submit_AggregatesOnlySubmittedSessionsAndCommitsBothStatesTogether()
    {
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([
            Session("first", true, Item("salad", 2)), Session("second", true, Item("salad", 1), Item("soup", 2)), Session("draft", false, Item("soup", 100))
        ]);
        var result = await service.SubmitAsync("owner", "group", "reservation");
        Assert.Equal(2, result.Count);
        Assert.Equal(3, result.Single(item => item.DishId == "salad").Quantity);
        Assert.Equal(2, result.Single(item => item.DishId == "soup").Quantity);
        transactions.Verify(repo => repo.SubmitAsync(It.Is<Order>(value => value.Status == OrderStatus.SubmittedByCustomer && value.Dishes.Count == 2), It.Is<GroupReservation>(value => value.Status == GroupReservationStatus.Submitted), order, It.IsAny<IReadOnlyList<GuestSession>>()), Times.Once);
        orders.Verify(repo => repo.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        groups.Verify(repo => repo.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    [Fact]
    public async Task Submit_RejectsReservationFromAnotherGroup()
    {
        await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "different-reservation"));
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Submit_MissingReservationReturnsNotFound()
    {
        reservations.Setup(repo => repo.GetReservationByIdAsync("reservation")).ReturnsAsync((Reservation?)null);
        await Assert.ThrowsAsync<ReservationNotFoundException>(() => service.SubmitAsync("owner", "group", "reservation"));
    }

    [Fact]
    public async Task Submit_MissingGroupReturnsNotFound()
    {
        groups.Setup(repo => repo.GetGroupReservationByIdAsync("group")).ReturnsAsync((GroupReservation?)null);
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => service.SubmitAsync("owner", "group", "reservation"));
    }

    [Fact]
    public async Task Submit_RejectsNoSubmittedSessions()
    {
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("draft", false, Item("salad", 1))]);
        await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "reservation"));
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Submit_RejectsEmptySubmittedCarts()
    {
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("empty", true)]);
        await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "reservation"));
    }

    [Fact]
    public async Task Submit_RejectsMissingOrder()
    {
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("first", true, Item("salad", 1))]);
        orders.Setup(repo => repo.GetOrderByReservationIdAsync("reservation")).ReturnsAsync((Order?)null);
        await Assert.ThrowsAsync<ValidationException>(() => service.SubmitAsync("owner", "group", "reservation"));
    }

    [Fact]
    public async Task Submit_RejectsRepeatedSubmission()
    {
        group.Status = GroupReservationStatus.Submitted;
        await Assert.ThrowsAsync<GroupOrderConflictException>(() => service.SubmitAsync("owner", "group", "reservation"));
        transactions.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_RejectsSessionFromDifferentGroup()
    {
        var session = Session("guest", false);
        session.GroupReservationId = "another-group";
        sessions.Setup(repo => repo.GetByGuestSessionId("guest")).ReturnsAsync(session);
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteSessionAsync("owner", "group", "guest"));
        sessions.Verify(repo => repo.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task Delete_RejectsOwnerSession()
    {
        var session = Session("owner-session", false);
        session.IsOwnerSession = true;
        sessions.Setup(repo => repo.GetByGuestSessionId("owner-session")).ReturnsAsync(session);
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteSessionAsync("owner", "group", "owner-session"));
    }

    [Fact]
    public async Task Delete_ReturnsSnapshotAfterDeletion()
    {
        var removed = Session("removed", true, Item("salad", 4));
        sessions.Setup(repo => repo.GetByGuestSessionId("removed")).ReturnsAsync(removed);
        sessions.Setup(repo => repo.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([Session("remaining", false, Item("soup", 1))]);
        var result = await service.DeleteSessionAsync("owner", "group", "removed");
        Assert.Equal(220m, result.Total);
        Assert.Equal("remaining", Assert.Single(result.Participants).Id);
        transactions.Verify(repo => repo.DeleteSessionAsync(removed, group), Times.Once);
    }
}
