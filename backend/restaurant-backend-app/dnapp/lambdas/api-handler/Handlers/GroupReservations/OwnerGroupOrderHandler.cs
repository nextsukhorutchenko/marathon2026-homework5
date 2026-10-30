using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GroupReservations;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class OwnerGroupOrderHandler(IOwnerGroupOrderService service) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var claims = request.RequestContext?.Authorizer?.Claims;
        if (claims is null || !claims.TryGetValue("sub", out var userId) || string.IsNullOrWhiteSpace(userId))
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        try
        {
            if (request.TryGetPathValue("reservationId", out var reservationId))
            {
                if (request.HttpMethod == "GET")
                    return ResponseBuilder.CreateJsonResponse(200, await service.GetOwnerSessionAsync(userId, reservationId));
                if (request.HttpMethod == "POST")
                    return ResponseBuilder.CreateJsonResponse(200, await service.PrepareAsync(userId, reservationId));
            }
            if (!request.TryGetPathValue("groupReservationId", out var groupReservationId) || string.IsNullOrWhiteSpace(groupReservationId))
                return ResponseBuilder.CreateErrorResponse(400, "Group reservation ID is required.", context.Logger);
            if (request.HttpMethod == "GET")
                return ResponseBuilder.CreateJsonResponse(200, await service.GetSessionsAsync(userId, groupReservationId));
            if (request.HttpMethod == "DELETE" && request.TryGetPathValue("guestSessionId", out var guestSessionId))
                return ResponseBuilder.CreateJsonResponse(200, await service.DeleteSessionAsync(userId, groupReservationId, guestSessionId));
            if (request.HttpMethod != "POST" || string.IsNullOrWhiteSpace(request.Body))
                return ResponseBuilder.CreateErrorResponse(400, "Invalid group order request.", context.Logger);
            var body = JsonSerializer.Deserialize<SubmitGroupOrderRequest>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (body is null || string.IsNullOrWhiteSpace(body.ReservationId))
                return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);
            var result = await service.SubmitAsync(userId, groupReservationId, body.ReservationId);
            return ResponseBuilder.CreateJsonResponse(200, result.ConvertAll(item => new { item.DishId, item.Quantity }));
        }
        catch (JsonException)
        {
            return ResponseBuilder.CreateErrorResponse(400, "Invalid JSON request body.", context.Logger);
        }
        catch (ActionForbiddenException exception)
        {
            return ResponseBuilder.CreateErrorResponse(403, exception.Message, context.Logger);
        }
        catch (Exception exception) when (exception is ReservationNotFoundException or GroupReservationNotFoundException or GuestSessionNotFoundException)
        {
            return ResponseBuilder.CreateErrorResponse(404, exception.Message, context.Logger);
        }
        catch (GroupOrderConflictException exception)
        {
            return ResponseBuilder.CreateErrorResponse(409, exception.Message, context.Logger);
        }
        catch (ValidationException exception)
        {
            return ResponseBuilder.CreateErrorCodeResponse(exception);
        }
    }
}
