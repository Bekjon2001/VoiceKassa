using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Interfaces;
using VoiceKassa.Application.Services;
using VoiceKassa.Domain.Entities;
using VoiceKassa.Domain.Enums;
using Xunit;

namespace VoiceKassa.Api.Tests;

public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _authRepoMock;
    private readonly Mock<IBusinessRepository> _businessRepoMock;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _authRepoMock = new Mock<IAuthRepository>();
        _businessRepoMock = new Mock<IBusinessRepository>();
        _authService = new AuthService(_authRepoMock.Object, _businessRepoMock.Object);
    }

    #region CheckSuperAdminExistsAsync

    [Fact]
    public async Task CheckSuperAdminExistsAsync_ReturnsRepoResult()
    {
        _authRepoMock.Setup(r => r.AnySuperAdminExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var result = await _authService.CheckSuperAdminExistsAsync();
        Assert.True(result.Exists);
    }

    #endregion

    #region SuperAdminLoginAsync

    [Fact]
    public async Task SuperAdminLoginAsync_ValidCredentials_ReturnsToken()
    {
        var account = new UserAccount
        {
            Id = 1, Login = "admin",
            PasswordHash = PasswordHasher.Hash("secret123"),
            IsSuperAdmin = true, IsActive = true, FullName = "Admin User",
        };
        _authRepoMock.Setup(r => r.GetUserAccountByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _authRepoMock.Setup(r => r.UpdateUserAccessTokenAsync(1, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (success, error, result) = await _authService.SuperAdminLoginAsync(
            new LoginRequest { Login = "admin", Password = "secret123" });

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.True(result.IsSuperAdmin);
        Assert.Equal("Admin User", result.FullName);
    }

    [Fact]
    public async Task SuperAdminLoginAsync_WrongPassword_ReturnsFalse()
    {
        var account = new UserAccount
        {
            Id = 1, Login = "admin",
            PasswordHash = PasswordHasher.Hash("secret123"),
            IsSuperAdmin = true, IsActive = true,
        };
        _authRepoMock.Setup(r => r.GetUserAccountByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var (success, error, result) = await _authService.SuperAdminLoginAsync(
            new LoginRequest { Login = "admin", Password = "wrong" });

        Assert.False(success);
        Assert.Equal("Login yoki parol noto'g'ri.", error);
        Assert.Null(result);
    }

    [Fact]
    public async Task SuperAdminLoginAsync_NotSuperAdmin_ReturnsFalse()
    {
        var account = new UserAccount
        {
            Id = 1, Login = "user",
            PasswordHash = PasswordHasher.Hash("secret123"),
            IsSuperAdmin = false, IsActive = true,
        };
        _authRepoMock.Setup(r => r.GetUserAccountByLoginAsync("user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var (success, error, result) = await _authService.SuperAdminLoginAsync(
            new LoginRequest { Login = "user", Password = "secret123" });

        Assert.False(success);
        Assert.Null(result);
    }

    #endregion

    #region OwnerLoginAsync

    [Fact]
    public async Task OwnerLoginAsync_ValidCredentials_ReturnsToken()
    {
        var owner = new RestaurantOwner
        {
            Id = 10, BusinessId = 42, Login = "owner1",
            PasswordHash = PasswordHasher.Hash("ownerpass"),
            IsActive = true, SubscriptionEndsAt = DateTime.UtcNow.AddDays(30),
            FullName = "Owner Name",
        };
        var business = new Business { Id = 42, Name = "Test Restaurant", Type = BusinessType.Restaurant, IsActive = true };

        _authRepoMock.Setup(r => r.GetOwnerByLoginAsync("owner1", It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        _businessRepoMock.Setup(r => r.GetBusinessByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(business);
        _authRepoMock.Setup(r => r.UpdateOwnerAccessTokenAsync(10, It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var (success, error, result) = await _authService.OwnerLoginAsync(
            new LoginRequest { Login = "owner1", Password = "ownerpass" });

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.Equal(42, result.BusinessId);
        Assert.Equal("Test Restaurant", result.RestaurantName);
        Assert.True(result.IsSubscriptionActive);
    }

    [Fact]
    public async Task OwnerLoginAsync_ExpiredSubscription_ReturnsFalse()
    {
        var owner = new RestaurantOwner
        {
            Id = 10, BusinessId = 42, Login = "owner1",
            PasswordHash = PasswordHasher.Hash("ownerpass"),
            IsActive = true, SubscriptionEndsAt = DateTime.UtcNow.AddDays(-1),
        };
        var business = new Business { Id = 42, Name = "Test Restaurant", Type = BusinessType.Restaurant, IsActive = true };

        _authRepoMock.Setup(r => r.GetOwnerByLoginAsync("owner1", It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        _businessRepoMock.Setup(r => r.GetBusinessByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(business);

        var (success, error, result) = await _authService.OwnerLoginAsync(
            new LoginRequest { Login = "owner1", Password = "ownerpass" });

        Assert.False(success);
        Assert.Contains("Obuna muddati tugagan", error);
        Assert.Null(result);
    }

    [Fact]
    public async Task OwnerLoginAsync_InactiveOwner_ReturnsFalse()
    {
        var owner = new RestaurantOwner
        {
            Id = 10, BusinessId = 42, Login = "owner1",
            PasswordHash = PasswordHasher.Hash("ownerpass"),
            IsActive = false, SubscriptionEndsAt = DateTime.UtcNow.AddDays(30),
        };
        _authRepoMock.Setup(r => r.GetOwnerByLoginAsync("owner1", It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var (success, error, result) = await _authService.OwnerLoginAsync(
            new LoginRequest { Login = "owner1", Password = "ownerpass" });

        Assert.False(success);
        Assert.Null(result);
    }

    #endregion

    #region ValidateOwnerTokenAsync

    [Fact]
    public async Task ValidateOwnerTokenAsync_ValidToken_ReturnsOwner()
    {
        var owner = new RestaurantOwner { Id = 10, BusinessId = 42, Login = "owner1", IsActive = true, SubscriptionEndsAt = DateTime.UtcNow.AddDays(30) };
        _authRepoMock.Setup(r => r.GetOwnerByTokenAsync("valid-token", It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var result = await _authService.ValidateOwnerTokenAsync("valid-token");
        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal(42, result.BusinessId);
    }

    [Fact]
    public async Task ValidateOwnerTokenAsync_ExpiredSubscription_ReturnsNull()
    {
        var owner = new RestaurantOwner { Id = 10, BusinessId = 42, Login = "owner1", IsActive = true, SubscriptionEndsAt = DateTime.UtcNow.AddDays(-1) };
        _authRepoMock.Setup(r => r.GetOwnerByTokenAsync("expired-token", It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var result = await _authService.ValidateOwnerTokenAsync("expired-token");
        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateOwnerTokenAsync_UnknownToken_ReturnsNull()
    {
        _authRepoMock.Setup(r => r.GetOwnerByTokenAsync("unknown-token", It.IsAny<CancellationToken>())).ReturnsAsync((RestaurantOwner?)null);
        var result = await _authService.ValidateOwnerTokenAsync("unknown-token");
        Assert.Null(result);
    }

    #endregion
}