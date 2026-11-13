using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Application.Services;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;
using Xunit;

namespace VoiceKassa.Api.Tests;

public class StaffRoleTests
{
    [Fact]
    public async Task LoginStaffAsync_ValidAccountIssuesRestaurantScopedToken()
    {
        var staff = new Staff
        {
            Id = 12,
            BusinessId = 7,
            FullName = "Ali Valiyev",
            Login = "ali",
            PasswordHash = PasswordHasher.Hash("secure-pass-1"),
            Role = StaffRole.Cashier,
            IsActive = true,
        };
        var business = new Business { Id = 7, Name = "Burger Zone", IsActive = true };
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.GetStaffByLoginAsync("ali", It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);
        repository.Setup(repo => repo.GetBusinessByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(business);
        repository.Setup(repo => repo.GetOwnerByBusinessIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestaurantOwner { BusinessId = 7, SubscriptionEndsAt = DateTime.UtcNow.AddDays(10) });
        repository.Setup(repo => repo.UpdateStaffAccessTokenAsync(12, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var issuer = new Mock<IAccessTokenIssuer>();
        issuer.Setup(tokenIssuer => tokenIssuer.CreateStaffToken(
                12, "cashier", 7, It.IsAny<string>()))
            .Returns("staff-jwt");
        var service = new BusinessService(
            repository.Object,
            NullLogger<BusinessService>.Instance,
            issuer.Object);

        var (success, error, result) = await service.LoginStaffAsync(
            new LoginRequest { Login = "ALI", Password = "secure-pass-1" });

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("staff-jwt", result.AccessToken);
        Assert.Equal(7, result.BusinessId);
        Assert.Equal("cashier", result.Role);
        issuer.Verify(tokenIssuer => tokenIssuer.CreateStaffToken(12, "cashier", 7, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CashierTokenCannotUseManagerAccess_AndIsBoundToItsBusiness()
    {
        const string token = "cashier-session";
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.GetStaffByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff
            {
                Id = 12,
                BusinessId = 7,
                Role = StaffRole.Cashier,
                IsActive = true,
                AccessToken = token,
            });
        repository.Setup(repo => repo.GetBusinessByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = 7, IsActive = true });
        repository.Setup(repo => repo.GetOwnerByBusinessIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestaurantOwner { BusinessId = 7, SubscriptionEndsAt = DateTime.UtcNow.AddDays(10) });
        var service = new BusinessService(
            repository.Object,
            NullLogger<BusinessService>.Instance,
            Mock.Of<IAccessTokenIssuer>());

        var ownerAccess = await service.AuthorizeOwnerAsync(7, token);
        var cashierAccess = await service.AuthorizeBusinessStaffAsync(7, token, default, StaffRole.Cashier);
        var waiterAccess = await service.AuthorizeBusinessStaffAsync(7, token, default, StaffRole.Waiter);
        var otherBusinessAccess = await service.AuthorizeBusinessStaffAsync(8, token, default, StaffRole.Cashier);

        Assert.False(ownerAccess.Success);
        Assert.True(cashierAccess.Success);
        Assert.False(waiterAccess.Success);
        Assert.False(otherBusinessAccess.Success);
    }

    [Fact]
    public async Task ManagerTokenCanUseFullBusinessAccess()
    {
        const string token = "manager-session";
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.GetStaffByTokenAsync(token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Staff
            {
                Id = 13,
                BusinessId = 7,
                Role = StaffRole.Manager,
                IsActive = true,
                AccessToken = token,
            });
        repository.Setup(repo => repo.GetBusinessByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Business { Id = 7, IsActive = true });
        repository.Setup(repo => repo.GetOwnerByBusinessIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RestaurantOwner { BusinessId = 7, SubscriptionEndsAt = DateTime.UtcNow.AddDays(10) });
        var service = new BusinessService(
            repository.Object,
            NullLogger<BusinessService>.Instance,
            Mock.Of<IAccessTokenIssuer>());

        var access = await service.AuthorizeOwnerAsync(7, token);

        Assert.True(access.Success);
    }

    [Fact]
    public async Task UpdateStaffCredentialsAsync_UpdatesLoginAndStoresOnlyPasswordHash()
    {
        var staff = new Staff
        {
            Id = 21,
            BusinessId = 7,
            FullName = "Ali Valiyev",
            Login = null,
            Role = StaffRole.Cashier,
            IsActive = true,
        };
        var repository = new Mock<IBusinessRepository>();
        repository.Setup(repo => repo.GetStaffByIdAsync(21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staff);
        repository.Setup(repo => repo.IsStaffLoginTakenAsync("ali.cashier", 21, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        repository.Setup(repo => repo.UpdateStaffCredentialsAsync(
                21, "ali.cashier", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<long, string, string, CancellationToken>((_, login, hash, _) =>
            {
                staff.Login = login;
                staff.PasswordHash = hash;
            })
            .ReturnsAsync(true);
        var service = new BusinessService(
            repository.Object,
            NullLogger<BusinessService>.Instance,
            Mock.Of<IAccessTokenIssuer>());

        var (success, error, result) = await service.UpdateStaffCredentialsAsync(
            21,
            new UpdateStaffCredentialsRequest { Login = " Ali.Cashier ", Password = "new-secure-pass" });

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("ali.cashier", result.Login);
        Assert.True(PasswordHasher.Verify("new-secure-pass", staff.PasswordHash!));
        Assert.NotEqual("new-secure-pass", staff.PasswordHash);
    }

    [Fact]
    public async Task MarkOrderReadyAsync_OnlyTransitionsInProgressOrders()
    {
        var order = new Order
        {
            Id = 44,
            BusinessId = 7,
            Status = OrderStatus.InProgress,
            Items = new List<OrderItem>(),
        };
        var repository = new Mock<IOrderRepository>();
        repository.Setup(repo => repo.GetByIdAsync(44, It.IsAny<CancellationToken>()))
            .ReturnsAsync(order);
        repository.Setup(repo => repo.UpdateOrderStatusAsync(
                44, OrderStatus.Ready, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var service = new OrderService(
            repository.Object,
            Mock.Of<IBusinessRepository>(),
            Mock.Of<IReservationRepository>(),
            Mock.Of<IAiExtractionService>(),
            NullLogger<OrderService>.Instance);

        var (success, error, result) = await service.MarkOrderReadyAsync(44);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(OrderStatus.Ready, result.Status);
    }
}
