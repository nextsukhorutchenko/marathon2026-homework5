using APIHandler.Entities;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupOwnerSessionTests
{
    [Fact]
    public async Task CreatingGroupAlsoCreatesOwnerSession()
    {
        var groups = new Mock<IGroupReservationRepository>();
        var reservations = new Mock<IReservationRepository>();
        var sessions = new Mock<IGuestSessionRepository>();
        reservations.Setup(repo => repo.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation {
            Id = "reservation", CustomerId = "owner", TableId = "table", WaiterId = "waiter"
        });
        var service = new GroupReservationService(groups.Object, Mock.Of<ITableRepository>(), reservations.Object, sessions.Object, Mock.Of<IUserService>(), Mock.Of<ILocationService>());
        var group = await service.CreateGroupReservationAsync("reservation");
        sessions.Verify(repo => repo.SaveAsync(It.Is<GuestSession>(session => session.IsOwnerSession && session.OwnerUserId == "owner" && session.Id == "owner-" + group.GroupReservationId)), Times.Once);
    }

    [Fact]
    public async Task EnsuringExistingGroupRepairsMissingOwnerSession()
    {
        var groups = new Mock<IGroupReservationRepository>();
        var sessions = new Mock<IGuestSessionRepository>();
        groups.Setup(repo => repo.GetByReservationIdAsync("reservation")).ReturnsAsync(new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "invite" });
        var service = new GroupReservationService(groups.Object, Mock.Of<ITableRepository>(), Mock.Of<IReservationRepository>(), sessions.Object, Mock.Of<IUserService>(), Mock.Of<ILocationService>());
        await service.CreateGroupReservationIfDoesNotExistAsync("owner", "reservation");
        sessions.Verify(repo => repo.SaveAsync(It.Is<GuestSession>(session => session.IsOwnerSession && session.OwnerUserId == "owner")), Times.Once);
    }
}
