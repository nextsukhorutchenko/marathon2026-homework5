using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Dishes;
using APIHandler.Services.Notifications;
using APIHandler.Services.Orders;
using Moq;

namespace APIHandler.Tests.Services;

public class ConsolidatedOrderGuardTests
{
    [Theory]
    [InlineData("unsubmit")]
    [InlineData("edit")]
    [InlineData("cancel")]
    [InlineData("submit")]
    public async Task IndividualCartCannotChangeSubmittedGroupOrder(string action)
    {
        var orders = new Mock<IOrderRepository>();
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(repo => repo.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table", StartTime = DateTime.UtcNow.AddDays(1) });
        orders.Setup(repo => repo.GetOrderByReservationIdAsync("reservation")).ReturnsAsync(new Order { OrderId = "order", ReservationId = "reservation", CustomerId = "owner", ReservationAddress = "Address", ReservationTime = "Tomorrow", Dishes = [new() { DishId = "salad", Quantity = 1 }], GroupOwnerDishes = [], Status = OrderStatus.SubmittedByCustomer });
        var dishes = new Mock<IDishService>();
        dishes.Setup(value => value.GetDishByIdAsync("salad")).ReturnsAsync(new Dish { Id = "salad", State = APIHandler.Constants.Dishes.DishState.Available });
        var service = new OrderService(orders.Object, reservations.Object, dishes.Object, Mock.Of<INotificationService>());
        var context = Mock.Of<ILambdaContext>(value => value.Logger == Mock.Of<ILambdaLogger>());
        await Assert.ThrowsAsync<ValidationException>(async () => {
            switch (action) {
                case "unsubmit": await service.UnSubmitPreOrderAsync("owner", "reservation", context); break;
                case "edit": await service.EditPreOrderAsync("owner", "reservation", [new() { DishId = "salad", Quantity = 1 }], context); break;
                case "cancel": await service.CancelPreOrderAsync("owner", "reservation", context); break;
                case "submit": await service.SubmitPreOrderAsync("owner", "reservation", context); break;
            }
        });
        orders.Verify(repo => repo.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }
}
