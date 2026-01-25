using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class ExitCodesTests
{
    [Fact]
    public void Success_IsZero()
    {
        ExitCodes.Success.ShouldBe(0);
    }

    [Fact]
    public void GeneralError_IsOne()
    {
        ExitCodes.GeneralError.ShouldBe(1);
    }

    [Fact]
    public void InvalidArguments_IsTwo()
    {
        ExitCodes.InvalidArguments.ShouldBe(2);
    }

    [Fact]
    public void AuthenticationError_IsThree()
    {
        ExitCodes.AuthenticationError.ShouldBe(3);
    }

    [Fact]
    public void NetworkError_IsFour()
    {
        ExitCodes.NetworkError.ShouldBe(4);
    }

    [Fact]
    public void CopilotError_IsFive()
    {
        ExitCodes.CopilotError.ShouldBe(5);
    }

    [Fact]
    public void Cancelled_Is130()
    {
        // 128 + SIGINT (2) = 130 is the Unix convention
        ExitCodes.Cancelled.ShouldBe(130);
    }

    [Theory]
    [InlineData(0, "Success")]
    [InlineData(1, "General error")]
    [InlineData(2, "Invalid arguments")]
    [InlineData(3, "Authentication error")]
    [InlineData(4, "Network error")]
    [InlineData(5, "Copilot SDK error")]
    [InlineData(6, "Configuration error")]
    [InlineData(130, "Operation cancelled")]
    public void GetDescription_ReturnsCorrectDescription(int code, string expected)
    {
        ExitCodes.GetDescription(code).ShouldBe(expected);
    }

    [Fact]
    public void GetDescription_UnknownCode_ReturnsUnknown()
    {
        ExitCodes.GetDescription(99).ShouldContain("Unknown");
        ExitCodes.GetDescription(99).ShouldContain("99");
    }
}
