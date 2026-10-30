using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GroupReservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Services;

public class OwnerGroupOrderHandlerTests
{
    private readonly Mock<IOwnerGroupOrderService> service = new();
    private readonly ILambdaContext context = Mock.Of<ILambdaContext>(value => value.Logger == Mock.Of<ILambdaLogger>());
    private APIGatewayProxyRequest Request(string method = "GET", string? body = null) => new() {
        HttpMethod = method, Body = body, PathParameters = new Dictionary<string, string> { ["groupReservationId"] = "group" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext {
            Authorizer = new APIGatewayCustomAuthorizerContext { Claims = new Dictionary<string, string> { ["sub"] = "owner" } }
        }
    };

    [Fact]
    public async Task AnonymousRequestIsUnauthorized()
    {
        var request = Request();
        request.RequestContext.Authorizer = null;
        Assert.Equal(401, (await new OwnerGroupOrderHandler(service.Object).HandleRequest(request, context)).StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SessionSnapshotUsesCamelCaseContract()
    {
        service.Setup(value => value.GetSessionsAsync("owner", "group")).ReturnsAsync(new GetAllSessionsResponse([], 0));
        var response = await new OwnerGroupOrderHandler(service.Object).HandleRequest(Request(), context);
        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(0, json.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal(JsonValueKind.Array, json.RootElement.GetProperty("participants").ValueKind);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"reservationId\":\" \"}")]
    public async Task MalformedSubmitReturnsBadRequest(string body)
    {
        Assert.Equal(400, (await new OwnerGroupOrderHandler(service.Object).HandleRequest(Request("POST", body), context)).StatusCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SubmitReturnsDishIdsAndQuantitiesOnly()
    {
        service.Setup(value => value.SubmitAsync("owner", "group", "reservation")).ReturnsAsync([new() { DishId = "salad", Quantity = 3 }]);
        var response = await new OwnerGroupOrderHandler(service.Object).HandleRequest(Request("POST", "{\"reservationId\":\"reservation\"}"), context);
        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(3, json.RootElement[0].GetProperty("quantity").GetInt32());
        Assert.False(json.RootElement[0].TryGetProperty("state", out _));
    }

    [Fact]
    public async Task OwnershipFailureMapsToForbidden()
    {
        service.Setup(value => value.GetSessionsAsync("owner", "group")).ThrowsAsync(new ActionForbiddenException("Forbidden"));
        Assert.Equal(403, (await new OwnerGroupOrderHandler(service.Object).HandleRequest(Request(), context)).StatusCode);
    }
}
