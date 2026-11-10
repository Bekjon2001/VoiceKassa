using VoiceKassa.Application.Services;
using Xunit;

namespace VoiceKassa.Api.Tests;

public class AuthLimiterTests
{
    [Fact]
    public void TryEnter_AllowsRepeatedRequestsUntilFailuresAreRegistered()
    {
        var limiter = new AuthLimiter();

        for (var i = 0; i < 10; i++)
        {
            Assert.True(limiter.TryEnter("127.0.0.1"));
            limiter.RegisterSuccess("127.0.0.1");
        }
    }

    [Fact]
    public void TryEnter_BlocksAfterFiveFailedAttempts_AndSuccessResetsLimit()
    {
        var limiter = new AuthLimiter();

        for (var i = 0; i < 5; i++)
            limiter.RegisterFailure("127.0.0.1");

        Assert.False(limiter.TryEnter("127.0.0.1"));

        limiter.RegisterSuccess("127.0.0.1");

        Assert.True(limiter.TryEnter("127.0.0.1"));
    }
}
