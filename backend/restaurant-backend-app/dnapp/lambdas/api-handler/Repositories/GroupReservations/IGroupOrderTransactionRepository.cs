using APIHandler.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public interface IGroupOrderTransactionRepository
{
    Task SubmitAsync(Order order, GroupReservation group, Order expectedOrder, IReadOnlyList<GuestSession> expectedSessions);
    Task PrepareAsync(Order? order, GuestSession? ownerSession, GroupReservation group);
    Task DeleteSessionAsync(GuestSession session, GroupReservation group);
}
