using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Mappers;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestSessions;
using APIHandler.Models.Orders;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Locations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class OwnerGroupOrderService(
    IGroupReservationRepository groups,
    IReservationRepository reservations,
    IGuestSessionRepository sessions,
    IDishRepository dishes,
    IOrderRepository orders,
    IGroupOrderTransactionRepository transactions,
    ILocationService locations) : IOwnerGroupOrderService
{
    public async Task<GetAllSessionsResponse> GetSessionsAsync(string userId, string groupReservationId)
    {
        var (group, _) = await LoadOwnedGroupAsync(userId, groupReservationId);
        return await BuildSnapshotAsync(group, userId);
    }

    public async Task<OwnerSessionResponse> GetOwnerSessionAsync(string userId, string reservationId)
    {
        var reservation = await reservations.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        EnsureOwner(userId, reservation);
        var group = await groups.GetByReservationIdAsync(reservationId)
            ?? throw new GroupReservationNotFoundException();
        group = await groups.GetGroupReservationByIdAsync(group.GroupReservationId) ?? throw new GroupReservationNotFoundException();
        if (group.ReservationId != reservationId)
            throw new GroupReservationNotFoundException();
        var ownerSession = await sessions.GetByGuestSessionId($"owner-{group.GroupReservationId}");
        if (ownerSession is null)
        {
            ownerSession = (await sessions.GetAllByGroupReservationIdAsync(group.GroupReservationId))
                .FirstOrDefault(session => session.IsOwnerSession && session.OwnerUserId == userId);
        }
        if (ownerSession is null || !ownerSession.IsOwnerSession || ownerSession.OwnerUserId != userId || ownerSession.GroupReservationId != group.GroupReservationId)
            throw new GuestSessionNotFoundException("Owner session not found. Prepare the group pre-order to create it.");
        return new OwnerSessionResponse(ownerSession.Id, group.GroupReservationId, group.Status.ToString());
    }

    public async Task<OwnerSessionResponse> PrepareAsync(string userId, string reservationId)
    {
        var reservation = await reservations.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        EnsureOwner(userId, reservation);
        var group = await groups.GetByReservationIdAsync(reservationId)
            ?? throw new GroupReservationNotFoundException();
        group = await groups.GetGroupReservationByIdAsync(group.GroupReservationId) ?? throw new GroupReservationNotFoundException();
        if (group.ReservationId != reservationId)
            throw new GroupReservationNotFoundException();
        EnsureOpen(group, reservation);
        var owner = (await LoadFreshSessionsAsync(group.GroupReservationId)).FirstOrDefault(session => session.IsOwnerSession && session.OwnerUserId == userId)
            ?? await sessions.GetByGuestSessionId($"owner-{group.GroupReservationId}");
        if (owner is not null && (!owner.IsOwnerSession || owner.OwnerUserId != userId || owner.GroupReservationId != group.GroupReservationId))
            throw new ValidationException("Existing owner session does not match the reservation owner.");
        var newOwner = owner is null ? new GuestSession {
            Id = $"owner-{group.GroupReservationId}", GroupReservationId = group.GroupReservationId,
            IsOwnerSession = true, OwnerUserId = userId, GuestName = "Owner", Dishes = []
        } : null;
        Order? newOrder = null;
        if (await LoadOrderAsync(group, userId) is null)
        {
            var location = await locations.GetLocationByTableIdAsync(reservation.TableId)
                ?? throw new ValidationException("Reservation location not found.");
            newOrder = new Order {
                OrderId = $"group-{group.GroupReservationId}", ReservationId = reservationId, CustomerId = userId,
                ReservationAddress = location.Address,
                ReservationTime = $"{reservation.StartTime:MMM dd, yyyy, h:mm tt} - {reservation.EndTime:h:mm tt}",
                Dishes = [], Status = OrderStatus.Created
            };
        }
        await transactions.PrepareAsync(newOrder, newOwner, group);
        return new OwnerSessionResponse((owner ?? newOwner)!.Id, group.GroupReservationId, group.Status.ToString());
    }

    public async Task<List<OrderItem>> SubmitAsync(string userId, string groupReservationId, string reservationId)
    {
        var (group, reservation) = await LoadOwnedGroupAsync(userId, groupReservationId);
        if (group.ReservationId != reservationId)
            throw new ValidationException("Reservation does not belong to this group.");
        EnsureOpen(group, reservation);
        var submitted = (await LoadFreshSessionsAsync(groupReservationId))
            .Where(session => session.IsSubmitted).ToList();
        if (submitted.Count == 0)
            throw new ValidationException("There are no submitted sessions.");
        var items = submitted.SelectMany(session => session.Dishes ?? []).ToList();
        if (items.Count == 0)
            throw new ValidationException("Submitted sessions contain no dishes.");
        if (items.Any(item => string.IsNullOrWhiteSpace(item.DishId) || item.Quantity <= 0))
            throw new ValidationException("Submitted dishes must have valid IDs and positive quantities.");
        var order = await LoadOrderAsync(group, userId)
            ?? throw new ValidationException("Order not found. Prepare the group pre-order first.");
        if (order.CustomerId != userId || order.ReservationId != reservationId)
            throw new ActionForbiddenException("The order does not belong to this reservation owner.");
        if (order.Status != OrderStatus.Created && order.Status != OrderStatus.SubmittedByCustomer)
            throw new GroupOrderConflictException("The order is no longer editable.");
        if (submitted.Count > 97)
            throw new ValidationException("Too many submitted participant carts for one group order.");
        // Existing /orders dishes are the owner's personal contribution, not guest drafts.
        var consolidated = order.Dishes.Concat(items).GroupBy(item => item.DishId)
            .Select(grouping => new OrderItem { DishId = grouping.Key, Quantity = grouping.Sum(item => item.Quantity), State = OrderItemState.Pending }).ToList();
        var available = (await dishes.GetDishesByIdsAsync(consolidated.Select(item => item.DishId).ToArray())).ToDictionary(dish => dish.Id);
        if (consolidated.Any(item => !available.TryGetValue(item.DishId, out var dish) || dish.State != Constants.Dishes.DishState.Available))
            throw new ValidationException("One or more submitted dishes are no longer available.");
        var updatedOrder = new Order {
            OrderId = order.OrderId, CustomerId = order.CustomerId, ReservationId = order.ReservationId,
            ReservationAddress = order.ReservationAddress, ReservationTime = order.ReservationTime,
            Dishes = consolidated, GroupOwnerDishes = order.Dishes, Status = OrderStatus.SubmittedByCustomer
        };
        var updatedGroup = new GroupReservation {
            GroupReservationId = group.GroupReservationId, ReservationId = group.ReservationId,
            ReservationToken = group.ReservationToken, Status = GroupReservationStatus.Submitted
        };
        await transactions.SubmitAsync(updatedOrder, updatedGroup, order, submitted);
        return consolidated;
    }

    public async Task<GetAllSessionsResponse> DeleteSessionAsync(string userId, string groupReservationId, string guestSessionId)
    {
        var (group, reservation) = await LoadOwnedGroupAsync(userId, groupReservationId);
        EnsureOpen(group, reservation);
        var session = await sessions.GetByGuestSessionId(guestSessionId)
            ?? throw new GuestSessionNotFoundException("Guest session not found.");
        if (session.GroupReservationId != groupReservationId)
            throw new ValidationException("Guest session does not belong to this group reservation.");
        if (session.IsOwnerSession)
            throw new ValidationException("The owner session cannot be removed.");
        await transactions.DeleteSessionAsync(session, group);
        // Exclude the deleted ID even if the GSI has not propagated the deletion yet.
        return await BuildSnapshotAsync(group, userId, guestSessionId);
    }

    private async Task<(GroupReservation, Reservation)> LoadOwnedGroupAsync(string userId, string groupReservationId)
    {
        var group = await groups.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();
        var reservation = await reservations.GetReservationByIdAsync(group.ReservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        EnsureOwner(userId, reservation);
        return (group, reservation);
    }

    private static void EnsureOwner(string userId, Reservation reservation)
    {
        if (string.IsNullOrWhiteSpace(userId) || reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can perform this action.");
    }

    private static void EnsureOpen(GroupReservation group, Reservation reservation)
    {
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupOrderConflictException("This group reservation is no longer open for changes.");
        if (DateTime.UtcNow > reservation.StartTime.ToUniversalTime().AddMinutes(-30))
            throw new GroupOrderConflictException("Pre-orders can only be changed up to 30 minutes before the reservation starts.");
    }

    private async Task<List<GuestSession>> LoadFreshSessionsAsync(string groupReservationId)
    {
        var indexed = await sessions.GetAllByGroupReservationIdAsync(groupReservationId);
        var loaded = await Task.WhenAll(indexed.Select(session => sessions.GetByGuestSessionId(session.Id)));
        var result = loaded.Where(session => session is not null && session.GroupReservationId == groupReservationId).Select(session => session!).ToList();
        var owner = await sessions.GetByGuestSessionId($"owner-{groupReservationId}");
        if (owner is not null && owner.GroupReservationId == groupReservationId && owner.IsOwnerSession && result.All(session => session.Id != owner.Id))
            result.Add(owner);
        return result;
    }

    private async Task<Order?> LoadOrderAsync(GroupReservation group, string userId)
    {
        var indexed = await orders.GetOrderByReservationIdAsync(group.ReservationId);
        // Re-read the primary key: the GSI can lag both creation and cart edits.
        var order = indexed is null
            ? await orders.GetOrderByIdAsync($"group-{group.GroupReservationId}", userId)
            : await orders.GetOrderByIdAsync(indexed.OrderId, indexed.CustomerId);
        return order?.Status == OrderStatus.Cancelled ? null : order;
    }

    private async Task<GetAllSessionsResponse> BuildSnapshotAsync(GroupReservation group, string userId, string? excludedSessionId = null)
    {
        var participants = (await LoadFreshSessionsAsync(group.GroupReservationId))
            .Where(session => session.Id != excludedSessionId).OrderByDescending(session => session.IsOwnerSession).ThenBy(session => session.Id).ToList();
        var order = await LoadOrderAsync(group, userId);
        var ownerDishes = group.Status == GroupReservationStatus.Submitted ? order?.GroupOwnerDishes ?? [] : order?.Dishes ?? [];
        var dishIds = participants.SelectMany(session => session.Dishes ?? []).Concat(ownerDishes).Select(item => item.DishId).Distinct().ToArray();
        var catalog = dishIds.Length == 0 ? new Dictionary<string, Dish>() : (await dishes.GetDishesByIdsAsync(dishIds)).ToDictionary(dish => dish.Id);
        var result = participants.Select(session => {
            var sessionItems = (session.Dishes ?? []).Concat(session.IsOwnerSession ? ownerDishes : []);
            var items = sessionItems.GroupBy(item => item.DishId).Select(grouping => new OrderItemResponse {
                Dish = catalog.TryGetValue(grouping.Key, out var dish) ? DishMapper.ToDishResponse(dish) : throw new ValidationException("A participant's dish no longer exists. Remove the participant or update their cart."),
                Quantity = grouping.Sum(item => item.Quantity)
            }).ToArray();
            return new Participant(session.Id, session.IsOwnerSession ? ParticipantType.Owner : ParticipantType.Guest, session.GuestName, items, items.Sum(item => item.Quantity * item.Dish.Price), session.IsSubmitted);
        }).ToList();
        return new GetAllSessionsResponse(result, result.Sum(participant => participant.Total));
    }
}
