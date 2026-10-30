using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GroupReservations;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupOrderTransactionRepositoryTests
{
    [Fact]
    public async Task SubmitSerializesBothItemsAndConditionsIntoOneTransaction()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");
        Environment.SetEnvironmentVariable("reservationsTable", "reservations-test");
        Environment.SetEnvironmentVariable("guestSessionTable", "sessions-test");
        using var mappingClient = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
        using var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => mappingClient).Build();
        var client = new Mock<IAmazonDynamoDB>();
        TransactWriteItemsRequest? captured = null;
        client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request).ReturnsAsync(new TransactWriteItemsResponse());
        var repository = new GroupOrderTransactionRepository(client.Object, context);
        var order = new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "Address", ReservationTime = "Tomorrow", Status = OrderStatus.SubmittedByCustomer, Dishes = [new() { DishId = "salad", Quantity = 3 }] };
        var group = new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "invite", Status = GroupReservationStatus.Submitted };
        var expectedOrder = new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = [], Status = OrderStatus.Created };
        var session = new GuestSession { Id = "guest", GroupReservationId = "group", GuestName = "Guest", IsSubmitted = true, Dishes = [new() { DishId = "salad", Quantity = 3 }] };
        await repository.SubmitAsync(order, group, expectedOrder, [session]);
        Assert.NotNull(captured);
        Assert.Equal(4, captured.TransactItems.Count);
        var savedOrder = captured.TransactItems[0].Put;
        Assert.Equal("orders-test", savedOrder.TableName);
        Assert.Equal("3", savedOrder.Item["dishes"].L[0].M["quantity"].N);
        Assert.Equal(((int)OrderStatus.SubmittedByCustomer).ToString(), savedOrder.Item["status"].N);
        Assert.Contains("#status = :expected", savedOrder.ConditionExpression);
        Assert.Contains("#dishes = :dishes", savedOrder.ConditionExpression);
        Assert.Equal("0", savedOrder.ExpressionAttributeValues[":expected"].N);
        var savedGroup = captured.TransactItems[1].Put;
        Assert.Equal("groups-test", savedGroup.TableName);
        Assert.Equal("1", savedGroup.Item["status"].N);
        Assert.Contains("#status = :open", savedGroup.ConditionExpression);
        Assert.Contains("#owner = :owner", captured.TransactItems[2].ConditionCheck.ConditionExpression);
        Assert.Equal("guest", captured.TransactItems[3].ConditionCheck.Key["guestSessionId"].S);
        Assert.Contains("#dishes = :dishes", captured.TransactItems[3].ConditionCheck.ConditionExpression);
    }

    [Fact]
    public async Task PreparationAllowsRecreatingCancelledOrderButNeverOverwritesAnActiveOrder()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");
        using var mappingClient = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
        using var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => mappingClient).Build();
        var client = new Mock<IAmazonDynamoDB>();
        TransactWriteItemsRequest? captured = null;
        client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()))
            .Callback<TransactWriteItemsRequest, CancellationToken>((request, _) => captured = request).ReturnsAsync(new TransactWriteItemsResponse());
        var order = new Order { OrderId = "group-group", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = [] };
        var group = new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "invite" };
        await new GroupOrderTransactionRepository(client.Object, context).PrepareAsync(order, null, group);
        Assert.NotNull(captured);
        Assert.Equal(2, captured.TransactItems.Count);
        Assert.Contains("#status = :open", captured.TransactItems[0].ConditionCheck.ConditionExpression);
        Assert.Equal("attribute_not_exists(orderId) OR #status = :cancelled", captured.TransactItems[1].Put.ConditionExpression);
        Assert.Equal(((int)OrderStatus.Cancelled).ToString(), captured.TransactItems[1].Put.ExpressionAttributeValues[":cancelled"].N);
    }

    [Fact]
    public async Task ConditionalFailureMapsToConflictInsteadOfPartialFallbackWrites()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        Environment.SetEnvironmentVariable("groupReservationsTable", "groups-test");
        Environment.SetEnvironmentVariable("reservationsTable", "reservations-test");
        using var mappingClient = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
        using var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => mappingClient).Build();
        var client = new Mock<IAmazonDynamoDB>();
        client.Setup(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TransactionCanceledException("Conflict") {
            CancellationReasons = [new() { Code = "None" }, new() { Code = "ConditionalCheckFailed" }]
        });
        var repository = new GroupOrderTransactionRepository(client.Object, context);
        var order = new Order { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "Address", ReservationTime = "Tomorrow", Status = OrderStatus.SubmittedByCustomer, Dishes = [] };
        var group = new GroupReservation { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "invite", Status = GroupReservationStatus.Submitted };
        await Assert.ThrowsAsync<GroupOrderConflictException>(() => repository.SubmitAsync(order, group, order, []));
        client.Verify(value => value.TransactWriteItemsAsync(It.IsAny<TransactWriteItemsRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
