using VoiceKassa.Application.Services;
using Xunit;

namespace VoiceKassa.Api.Tests;

/// <summary>
/// Parolni hashlash/tekshirish xavfsizligi uchun unit testlar.
/// DB ga bog'liq emas — faqat PasswordHasher sinab chiqiladi.
/// </summary>
public class PasswordHasherTests
{
    [Fact]
    public void Hash_ReturnsSaltHashFormat()
    {
        // Arrange
        var password = "Voice2026!";

        // Act
        var hash = PasswordHasher.Hash(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.Matches(@"^[A-Za-z0-9+/=]+\.[A-Za-z0-9+/=]+$", hash);
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "Secret123!";
        var hash = PasswordHasher.Hash(password);

        // Act
        var result = PasswordHasher.Verify(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        // Arrange
        var hash = PasswordHasher.Hash("CorrectPassword!");

        // Act
        var result = PasswordHasher.Verify("WrongPassword!", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_NullHash_ReturnsFalse()
    {
        // Act
        var result = PasswordHasher.Verify("anything", null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Verify_EmptyHash_ReturnsFalse()
    {
        // Act
        var result = PasswordHasher.Verify("anything", "");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Hash_DifferentPasswords_ProduceDifferentHashes()
    {
        // Arrange
        var hash1 = PasswordHasher.Hash("passwordA");
        var hash2 = PasswordHasher.Hash("passwordB");

        // Act & Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProduceDifferentHashes_ThanksToSalt()
    {
        // Arrange
        var hash1 = PasswordHasher.Hash("samePassword");
        var hash2 = PasswordHasher.Hash("samePassword");

        // Act & Assert
        Assert.NotEqual(hash1, hash2);
    }
}