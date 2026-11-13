using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Application.Services;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;
using Xunit;

namespace VoiceKassa.Api.Tests;

public class ReservationTableSessionTests
{
    [Fact]
    public async Task EnterByCodeAsync_RejectsDifferentTableWhenCurrentSessionHasOpenOrder()
    {
        var reservationRepo = new Mock<IReservationRepository>();
        var businessRepo = new Mock<IBusinessRepository>();
        var currentReservation = new Reservation
        {
            Id = 42,
            BusinessId = 7,
            TableId = 91,
            SessionToken = "active-session",
            Status = ReservationStatus.Arrived,
        };
        var targetReservation = new Reservation
        {
            Id = 43,
            BusinessId = 7,
            TableId = 92,
            Code = "123456",
            Status = ReservationStatus.Waiting,
        };
        reservationRepo.Setup(repo => repo.GetByCodeAsync("123456", It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetReservation);
        reservationRepo.Setup(repo => repo.GetBySessionTokenAsync("active-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentReservation);
        businessRepo.Setup(repo => repo.HasOpenOrderForTableAsync(91, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ReservationService(
            reservationRepo.Object,
            businessRepo.Object,
            NullLogger<ReservationService>.Instance);

        var (success, error, result) = await service.EnterByCodeAsync(
            new EnterReservationRequest { Code = "123456" },
            sessionToken: "active-session");

        Assert.False(success);
        Assert.Null(result);
        Assert.Contains("boshqa stolni tanlab bo'lmaydi", error);
        reservationRepo.Verify(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SelectTableAsync_RejectsSwitchAfterOrderIsOpen()
    {
        var reservationRepo = new Mock<IReservationRepository>();
        var businessRepo = new Mock<IBusinessRepository>();
        var currentReservation = new Reservation
        {
            Id = 42,
            BusinessId = 7,
            TableId = 91,
            SessionToken = "active-session",
            Status = ReservationStatus.Arrived,
        };
        reservationRepo.Setup(repo => repo.GetBySessionTokenAsync("active-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentReservation);
        businessRepo.Setup(repo => repo.GetTableByIdAsync(92, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Table { Id = 92, BusinessId = 7, Name = "Stol 10" });
        businessRepo.Setup(repo => repo.HasOpenOrderForTableAsync(91, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ReservationService(
            reservationRepo.Object,
            businessRepo.Object,
            NullLogger<ReservationService>.Instance);

        var (success, error, result) = await service.SelectTableAsync(
            new SelectTableRequest { BusinessId = 7, TableId = 92 },
            sessionToken: "active-session");

        Assert.False(success);
        Assert.Null(result);
        Assert.Contains("boshqa stolni tanlab bo'lmaydi", error);
        reservationRepo.Verify(
            repo => repo.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SelectTableAsync_ReusesCurrentSessionWhenTableIsUnchanged()
    {
        var reservationRepo = new Mock<IReservationRepository>();
        var businessRepo = new Mock<IBusinessRepository>();
        var currentReservation = new Reservation
        {
            Id = 42,
            BusinessId = 7,
            TableId = 91,
            SessionToken = "active-session",
            Status = ReservationStatus.Arrived,
        };
        reservationRepo.Setup(repo => repo.GetBySessionTokenAsync("active-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentReservation);
        businessRepo.Setup(repo => repo.GetTableByIdAsync(91, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Table { Id = 91, BusinessId = 7, Name = "Stol 9" });

        var service = new ReservationService(
            reservationRepo.Object,
            businessRepo.Object,
            NullLogger<ReservationService>.Instance);

        var (success, error, result) = await service.SelectTableAsync(
            new SelectTableRequest { BusinessId = 7, TableId = 91 },
            sessionToken: "active-session");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("active-session", result.SessionToken);
        Assert.Equal(42, result.ReservationId);
        reservationRepo.Verify(
            repo => repo.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SelectTableAsync_CancelsPreviousReservationWhenSwitchingBeforeOrder()
    {
        var reservationRepo = new Mock<IReservationRepository>();
        var businessRepo = new Mock<IBusinessRepository>();
        var currentReservation = new Reservation
        {
            Id = 42,
            BusinessId = 7,
            TableId = 91,
            SessionToken = "active-session",
            Status = ReservationStatus.Arrived,
        };
        Reservation? addedReservation = null;

        reservationRepo.Setup(repo => repo.GetBySessionTokenAsync("active-session", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentReservation);
        reservationRepo.Setup(repo => repo.IsTableFreeAsync(92, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        reservationRepo.Setup(repo => repo.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);
        reservationRepo.Setup(repo => repo.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()))
            .Callback<Reservation, CancellationToken>((reservation, _) => addedReservation = reservation)
            .ReturnsAsync((Reservation reservation, CancellationToken _) => reservation);
        businessRepo.Setup(repo => repo.GetTableByIdAsync(92, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Table { Id = 92, BusinessId = 7, Name = "Stol 10" });
        businessRepo.Setup(repo => repo.HasOpenOrderForTableAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        reservationRepo.Setup(repo => repo.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new ReservationService(
            reservationRepo.Object,
            businessRepo.Object,
            NullLogger<ReservationService>.Instance);

        var (success, error, result) = await service.SelectTableAsync(
            new SelectTableRequest { BusinessId = 7, TableId = 92 },
            sessionToken: "active-session");

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(ReservationStatus.Cancelled, currentReservation.Status);
        Assert.Null(currentReservation.SessionToken);
        Assert.NotNull(addedReservation);
        Assert.Equal(92, addedReservation.TableId);
    }
}
