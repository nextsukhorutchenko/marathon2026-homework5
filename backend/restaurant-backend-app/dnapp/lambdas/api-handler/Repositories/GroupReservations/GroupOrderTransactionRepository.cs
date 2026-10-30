using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace APIHandler.Repositories.GroupReservations;

public class GroupOrderTransactionRepository(IAmazonDynamoDB client, IDynamoDBContext context) : IGroupOrderTransactionRepository
{
    private Dictionary<string, AttributeValue> Map<T>(T item, string table) =>
        context.ToDocument(item, new ToDocumentConfig { OverrideTableName = table }).ToAttributeMap();

    public async Task SubmitAsync(Order order, GroupReservation group, Order expectedOrder, IReadOnlyList<GuestSession> expectedSessions)
    {
        var expected = Map(expectedOrder, ResourceNames.OrdersTableName);
        var actions = new List<TransactWriteItem> {
            new() { Put = new Put {
                TableName = ResourceNames.OrdersTableName, Item = Map(order, ResourceNames.OrdersTableName),
                ConditionExpression = "attribute_exists(orderId) AND #status = :expected AND #dishes = :dishes",
                ExpressionAttributeNames = new() { ["#status"] = "status", ["#dishes"] = "dishes" },
                ExpressionAttributeValues = new() { [":expected"] = expected["status"], [":dishes"] = expected["dishes"] }
            } },
            new() { Put = new Put {
                TableName = ResourceNames.GroupReservationsTableName, Item = Map(group, ResourceNames.GroupReservationsTableName),
                ConditionExpression = "attribute_exists(groupReservationId) AND #status = :open",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":open"] = new() { N = ((int)GroupReservationStatus.Open).ToString() } }
            } },
            new() { ConditionCheck = new ConditionCheck {
                TableName = ResourceNames.ReservationsTableName,
                Key = new() { ["id"] = new() { S = group.ReservationId } },
                ConditionExpression = "#owner = :owner AND #status = :reserved",
                ExpressionAttributeNames = new() { ["#owner"] = "customerId", ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":owner"] = new() { S = order.CustomerId }, [":reserved"] = new() { N = ((int)ReservationStatus.Reserved).ToString() } }
            } }
        };
        foreach (var session in expectedSessions)
        {
            var mapped = Map(session, ResourceNames.GuestSessionTableName);
            actions.Add(new TransactWriteItem { ConditionCheck = new ConditionCheck {
                TableName = ResourceNames.GuestSessionTableName,
                Key = new() { ["guestSessionId"] = new() { S = session.Id } },
                ConditionExpression = "attribute_exists(guestSessionId) AND groupReservationId = :group AND #submitted = :submitted AND #dishes = :dishes",
                ExpressionAttributeNames = new() { ["#submitted"] = "isSubmitted", ["#dishes"] = "dishes" },
                ExpressionAttributeValues = new() { [":group"] = new() { S = group.GroupReservationId }, [":submitted"] = mapped["isSubmitted"], [":dishes"] = mapped["dishes"] }
            } });
        }
        await ExecuteAsync(actions);
    }

    public async Task PrepareAsync(Order? order, GuestSession? ownerSession, GroupReservation group)
    {
        var actions = new List<TransactWriteItem> { CheckOpen(group) };
        if (order is not null)
            actions.Add(new() { Put = new Put {
                TableName = ResourceNames.OrdersTableName, Item = Map(order, ResourceNames.OrdersTableName),
                ConditionExpression = "attribute_not_exists(orderId) OR #status = :cancelled",
                ExpressionAttributeNames = new() { ["#status"] = "status" },
                ExpressionAttributeValues = new() { [":cancelled"] = new() { N = ((int)OrderStatus.Cancelled).ToString() } }
            } });
        if (ownerSession is not null)
            actions.Add(new() { Put = new Put { TableName = ResourceNames.GuestSessionTableName, Item = Map(ownerSession, ResourceNames.GuestSessionTableName), ConditionExpression = "attribute_not_exists(guestSessionId)" } });
        if (actions.Count > 1)
            await ExecuteAsync(actions);
    }

    public Task DeleteSessionAsync(GuestSession session, GroupReservation group) => ExecuteAsync([
        CheckOpen(group),
        new() { Delete = new Delete {
            TableName = ResourceNames.GuestSessionTableName,
            Key = new() { ["guestSessionId"] = new() { S = session.Id } },
            ConditionExpression = "attribute_exists(guestSessionId) AND groupReservationId = :group",
            ExpressionAttributeValues = new() { [":group"] = new() { S = group.GroupReservationId } }
        } }
    ]);

    private static TransactWriteItem CheckOpen(GroupReservation group) => new() { ConditionCheck = new ConditionCheck {
        TableName = ResourceNames.GroupReservationsTableName,
        Key = new() { ["groupReservationId"] = new() { S = group.GroupReservationId } },
        ConditionExpression = "attribute_exists(groupReservationId) AND #status = :open",
        ExpressionAttributeNames = new() { ["#status"] = "status" },
        ExpressionAttributeValues = new() { [":open"] = new() { N = ((int)GroupReservationStatus.Open).ToString() } }
    } };

    private async Task ExecuteAsync(List<TransactWriteItem> actions)
    {
        try { await client.TransactWriteItemsAsync(new TransactWriteItemsRequest { TransactItems = actions }); }
        catch (TransactionCanceledException exception) when (exception.CancellationReasons?.Any(reason => reason.Code is "ConditionalCheckFailed" or "TransactionConflict") == true)
        {
            throw new GroupOrderConflictException("The group, a cart, or the order has changed. Refresh before trying again.");
        }
    }
}
