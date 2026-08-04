namespace Lopen.Llm.Tests;

public class CopilotStartupFailureMapperTests
{
    [Fact]
    public void Classify_AuthKeyword_Unauthorized_ReturnsAuth()
    {
        var ex = new InvalidOperationException("HTTP 401 Unauthorized");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_AuthKeyword_Forbidden_ReturnsAuth()
    {
        var ex = new InvalidOperationException("403 Forbidden access denied");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_AuthKeyword_Token_ReturnsAuth()
    {
        var ex = new InvalidOperationException("Invalid GitHub token provided");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_AuthKeyword_Credentials_ReturnsAuth()
    {
        var ex = new InvalidOperationException("Missing credentials for Copilot");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_NetworkKeyword_Connection_ReturnsNetwork()
    {
        var ex = new InvalidOperationException("Connection refused by remote host");
        Assert.Equal(CopilotFailureCategory.Network, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_NetworkKeyword_Timeout_ReturnsNetwork()
    {
        var ex = new InvalidOperationException("Operation timeout after 30 seconds");
        Assert.Equal(CopilotFailureCategory.Network, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_NetworkKeyword_DNS_ReturnsNetwork()
    {
        var ex = new InvalidOperationException("DNS resolution failed for api.github.com");
        Assert.Equal(CopilotFailureCategory.Network, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_ServiceKeyword_Unavailable_ReturnsService()
    {
        var ex = new InvalidOperationException("Service unavailable at this time");
        Assert.Equal(CopilotFailureCategory.Service, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_ServiceKeyword_503_ReturnsService()
    {
        var ex = new InvalidOperationException("HTTP 503 Service Unavailable");
        Assert.Equal(CopilotFailureCategory.Service, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_UnknownMessage_ReturnsUnknown()
    {
        var ex = new InvalidOperationException("Something unexpected happened during startup");
        Assert.Equal(CopilotFailureCategory.Unknown, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_InnerExceptionMessage_IsIncluded()
    {
        var inner = new InvalidOperationException("Token expired");
        var outer = new InvalidOperationException("Startup failed", inner);
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(outer));
    }

    [Fact]
    public void Classify_AuthTakesPriorityOverNetwork()
    {
        // Both auth and network keywords present; auth should win (checked first)
        var ex = new InvalidOperationException("Unauthorized network connection refused");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void Classify_IsCaseInsensitive()
    {
        var ex = new InvalidOperationException("AUTHENTICATION FAILURE");
        Assert.Equal(CopilotFailureCategory.Auth, CopilotStartupFailureMapper.Classify(ex));
    }

    [Fact]
    public void CreateDiagnosticException_AuthFailure_HasAuthCategoryAndHint()
    {
        var inner = new InvalidOperationException("401 Unauthorized");
        LlmException result = CopilotStartupFailureMapper.CreateDiagnosticException("Failed to start", inner);

        Assert.Equal("Failed to start", result.Message);
        Assert.Same(inner, result.InnerException);
        Assert.Equal(CopilotFailureCategory.Auth, result.DiagnosticCategory);
        Assert.NotNull(result.UserHint);
        Assert.Contains("lopen auth login", result.UserHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDiagnosticException_NetworkFailure_HasNetworkCategoryAndHint()
    {
        var inner = new InvalidOperationException("Connection timeout");
        LlmException result = CopilotStartupFailureMapper.CreateDiagnosticException("Failed to start", inner);

        Assert.Equal(CopilotFailureCategory.Network, result.DiagnosticCategory);
        Assert.NotNull(result.UserHint);
        Assert.Contains("network", result.UserHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDiagnosticException_ServiceFailure_HasServiceCategoryAndHint()
    {
        var inner = new InvalidOperationException("503 Service Unavailable");
        LlmException result = CopilotStartupFailureMapper.CreateDiagnosticException("Failed to start", inner);

        Assert.Equal(CopilotFailureCategory.Service, result.DiagnosticCategory);
        Assert.NotNull(result.UserHint);
        Assert.Contains("Copilot service", result.UserHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDiagnosticException_UnknownFailure_HasUnknownCategoryAndHint()
    {
        var inner = new InvalidOperationException("Something went wrong");
        LlmException result = CopilotStartupFailureMapper.CreateDiagnosticException("Failed to start", inner);

        Assert.Equal(CopilotFailureCategory.Unknown, result.DiagnosticCategory);
        Assert.NotNull(result.UserHint);
        Assert.Contains("lopen auth status", result.UserHint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateDiagnosticException_NullModelIsSet()
    {
        var inner = new InvalidOperationException("error");
        LlmException result = CopilotStartupFailureMapper.CreateDiagnosticException("msg", inner);
        Assert.Null(result.Model);
    }
}
