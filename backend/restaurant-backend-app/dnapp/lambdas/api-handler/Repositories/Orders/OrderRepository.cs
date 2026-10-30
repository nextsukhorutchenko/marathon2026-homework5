using Amazon.DynamoDBv2.DataModel;
using APIHandler.Constants;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using APIHandler.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.Orders;

public class OrderRepository : IOrderRepository
{
    private readonly string OrdersTableName = ResourceNames.OrdersTableName;
    private readonly string CustomerIdGlobalIndex = ResourceNames.OrdersTableCustomerIdGlobalIndex;
    private readonly string ReservationIdGlobalIndex = ResourceNames.OrdersTableReservationIdGlobalIndex;

    private readonly IDynamoDBContext _dynamoDbContext;
    private readonly IAmazonDynamoDB _client;

    public OrderRepository(IDynamoDBContext dynamoDbContext, IAmazonDynamoDB client)
    {
        _client = client;
        _dynamoDbContext = dynamoDbContext ?? throw new ArgumentNullException(nameof(dynamoDbContext));
    }

    public async Task<Order?> GetOrderByIdAsync(string orderId, string customerId)
    {
        return await _dynamoDbContext.LoadAsync<Order>(orderId, customerId, DynamoConfig.LoadConfig(OrdersTableName));
    }

    public async Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = CustomerIdGlobalIndex,
        };

        var query = _dynamoDbContext.QueryAsync<Order>(customerId, config);

        return await query.GetRemainingAsync();
    }

    public async Task<Order?> GetOrderByReservationIdAsync(string reservationId)
    {
        var config = new QueryConfig
        {
            OverrideTableName = OrdersTableName,
            IndexName = ReservationIdGlobalIndex,
        };

        var results = await _dynamoDbContext.QueryAsync<Order>(reservationId, config).GetNextSetAsync();

        var indexed = results.FirstOrDefault(o => o.Status != OrderStatus.Cancelled);
        if (indexed is null) return null;
        var current = await GetOrderByIdAsync(indexed.OrderId, indexed.CustomerId);
        return current?.Status == OrderStatus.Cancelled ? null : current;
    }

    public async Task SaveOrderAsync(Order order)
    {
        await _dynamoDbContext.SaveAsync(order, DynamoConfig.SaveConfig(OrdersTableName));
    }

    public async Task SaveIndividualOrderAsync(Order order)
    {
        try
        {
            await _client.PutItemAsync(new PutItemRequest {
                TableName = OrdersTableName,
                Item = _dynamoDbContext.ToDocument(order, new ToDocumentConfig { OverrideTableName = OrdersTableName }).ToAttributeMap(),
                ConditionExpression = "attribute_exists(orderId) AND (attribute_not_exists(groupOwnerDishes) OR attribute_type(groupOwnerDishes, :nullType))",
                ExpressionAttributeValues = new() { [":nullType"] = new() { S = "NULL" } }
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new APIHandler.Exceptions.ValidationException("This order changed or was submitted as a group. Refresh before making changes.");
        }
    }
}
