using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Application.Services;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;
using Xunit;

namespace VoiceKassa.Api.Tests;

public class OrderCancellationSessionTests
{
    [Fact]
    public async Task CancelOrderAsync_LeavesReservationSessionActiveForRetry()
    {
        var orderRepository = new Mock<IOrderRepository>();
        var reservationRepository = new Mock<IReservationRepository>();
        var cancelledOrder = new Order
        {
            Id = 4,
            BusinessId = 7,
            TableId = 91,
            ReservationId = 31,
            Status = OrderStatus.Cancelled,
            Items = new List<OrderItem>(),
        };
        orderRepository.Setup(repo =>
                repo.CancelAtomicallyAsync(4, "Oshxona bekor qildi", It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, (string?)null, cancelledOrder));

        var service = new OrderService(
            orderRepository.Object,
            Mock.Of<IBusinessRepository>(),
            reservationRepository.Object,
            Mock.Of<IAiExtractionService>(),
            NullLogger<OrderService>.Instance);

        var (success, error, result) = await service.CancelOrderAsync(
            4,
            new CancelOrderRequest { Reason = "Oshxona bekor qildi" });

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        reservationRepository.Verify(
            repo => repo.GetByIdAsync(31, It.IsAny<CancellationToken>()),
            Times.Never);
        reservationRepository.Verify(
            repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

}
