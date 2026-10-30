using APIHandler.Entities;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestSessions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public interface IOwnerGroupOrderService
{
    Task<GetAllSessionsResponse> GetSessionsAsync(string userId, string groupReservationId);
    Task<OwnerSessionResponse> GetOwnerSessionAsync(string userId, string reservationId);
    Task<OwnerSessionResponse> PrepareAsync(string userId, string reservationId);
    Task<List<OrderItem>> SubmitAsync(string userId, string groupReservationId, string reservationId);
    Task<GetAllSessionsResponse> DeleteSessionAsync(string userId, string groupReservationId, string guestSessionId);
}
