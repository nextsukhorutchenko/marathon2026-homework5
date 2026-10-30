using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using APIHandler.Entities;
using APIHandler.Repositories.Orders;
using Moq;

namespace APIHandler.Tests.Services;

public class IndividualOrderWriteTests
{
    private static Order OrdinaryOrder() => new() { OrderId = "order", CustomerId = "owner", ReservationId = "reservation", ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = [], Status = OrderStatus.Created };

    [Fact]
    public async Task IndividualWritesCannotOverwriteAConsolidatedGroup()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        using var mappingClient = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
        using var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => mappingClient).Build();
        var client = new Mock<IAmazonDynamoDB>();
        PutItemRequest? captured = null;
        client.Setup(value => value.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PutItemRequest, CancellationToken>((request, _) => captured = request).ReturnsAsync(new PutItemResponse());
        await new OrderRepository(context, client.Object).SaveIndividualOrderAsync(OrdinaryOrder());
        Assert.NotNull(captured);
        Assert.Contains("attribute_not_exists(groupOwnerDishes)", captured.ConditionExpression);
        Assert.Equal("order", captured.Item["orderId"].S);
        Assert.Equal("NULL", captured.ExpressionAttributeValues[":nullType"].S);
    }

    [Fact]
    public async Task RejectedStaleIndividualWriteReturnsDomainValidationError()
    {
        Environment.SetEnvironmentVariable("ordersTable", "orders-test");
        using var mappingClient = new AmazonDynamoDBClient(new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
        using var context = new DynamoDBContextBuilder().WithDynamoDBClient(() => mappingClient).Build();
        var client = new Mock<IAmazonDynamoDB>();
        client.Setup(value => value.PutItemAsync(It.IsAny<PutItemRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ConditionalCheckFailedException("Group committed first"));
        await Assert.ThrowsAsync<APIHandler.Exceptions.ValidationException>(() => new OrderRepository(context, client.Object).SaveIndividualOrderAsync(OrdinaryOrder()));
    }
}
