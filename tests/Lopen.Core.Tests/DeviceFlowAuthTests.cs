using Shouldly;

namespace Lopen.Core.Tests;

public class DeviceFlowAuthTests
{
    [Fact]
    public void GetConfig_ReturnsNullForMissingFile()
    {
        var auth = new DeviceFlowAuth("/nonexistent/path/oauth.json");

        var config = auth.GetConfig();

        config.ShouldBeNull();
    }

    [Fact]
    public void OAuthAppConfig_DeserializesCorrectly()
    {
        var json = """
        {
            "client-id": "test-client-id",
            "client-secret": "test-secret",
            "redirect-uris": ["http://localhost"]
        }
        """;

        var config = System.Text.Json.JsonSerializer.Deserialize<OAuthAppConfig>(json);

        config.ShouldNotBeNull();
        config.ClientId.ShouldBe("test-client-id");
        config.ClientSecret.ShouldBe("test-secret");
        config.RedirectUris.ShouldNotBeNull();
        config.RedirectUris.ShouldContain("http://localhost");
    }

    [Fact]
    public void DeviceCodeResponse_DeserializesCorrectly()
    {
        var json = """
        {
            "device_code": "abc123",
            "user_code": "WXYZ-1234",
            "verification_uri": "https://github.com/login/device",
            "expires_in": 900,
            "interval": 5
        }
        """;

        var response = System.Text.Json.JsonSerializer.Deserialize<DeviceCodeResponse>(json);

        response.ShouldNotBeNull();
        response.DeviceCode.ShouldBe("abc123");
        response.UserCode.ShouldBe("WXYZ-1234");
        response.VerificationUri.ShouldBe("https://github.com/login/device");
        response.ExpiresIn.ShouldBe(900);
        response.Interval.ShouldBe(5);
    }

    [Fact]
    public void TokenResponse_DeserializesSuccessCorrectly()
    {
        var json = """
        {
            "access_token": "gho_abc123",
            "token_type": "bearer",
            "scope": "copilot read:user"
        }
        """;

        var response = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json);

        response.ShouldNotBeNull();
        response.AccessToken.ShouldBe("gho_abc123");
        response.TokenType.ShouldBe("bearer");
        response.Scope.ShouldBe("copilot read:user");
        response.Error.ShouldBeNull();
    }

    [Fact]
    public void TokenResponse_DeserializesErrorCorrectly()
    {
        var json = """
        {
            "error": "authorization_pending",
            "error_description": "Waiting for authorization"
        }
        """;

        var response = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(json);

        response.ShouldNotBeNull();
        response.AccessToken.ShouldBeNull();
        response.Error.ShouldBe("authorization_pending");
        response.ErrorDescription.ShouldBe("Waiting for authorization");
    }

    [Fact]
    public void DeviceFlowResult_SuccessCase()
    {
        var result = new DeviceFlowResult
        {
            Success = true,
            AccessToken = "token123",
            UserCode = "ABCD-1234",
            VerificationUri = "https://github.com/login/device"
        };

        result.Success.ShouldBeTrue();
        result.AccessToken.ShouldBe("token123");
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void DeviceFlowResult_FailureCase()
    {
        var result = new DeviceFlowResult
        {
            Success = false,
            Error = "access_denied"
        };

        result.Success.ShouldBeFalse();
        result.AccessToken.ShouldBeNull();
        result.Error.ShouldBe("access_denied");
    }
}

public class MockDeviceFlowAuthTests
{
    [Fact]
    public void GetConfig_ReturnsConfiguredValue()
    {
        var config = new OAuthAppConfig { ClientId = "test" };
        var mock = new MockDeviceFlowAuth().WithConfig(config);

        var result = mock.GetConfig();

        result.ShouldBe(config);
    }

    [Fact]
    public async Task StartDeviceFlowAsync_ReturnsConfiguredResponse()
    {
        var response = new DeviceCodeResponse
        {
            DeviceCode = "dev123",
            UserCode = "USER-CODE",
            VerificationUri = "https://example.com"
        };
        var mock = new MockDeviceFlowAuth().WithDeviceCodeResponse(response);

        var result = await mock.StartDeviceFlowAsync();

        mock.StartFlowWasCalled.ShouldBeTrue();
        result.ShouldBe(response);
    }

    [Fact]
    public async Task StartDeviceFlowAsync_ReturnsNullWhenConfiguredToFail()
    {
        var mock = new MockDeviceFlowAuth().ShouldFailToStart();

        var result = await mock.StartDeviceFlowAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task PollForTokenAsync_ReturnsConfiguredResult()
    {
        var deviceCode = new DeviceCodeResponse
        {
            DeviceCode = "dev123",
            UserCode = "USER-CODE",
            VerificationUri = "https://example.com"
        };
        var pollResult = new DeviceFlowResult { Success = true, AccessToken = "token123" };
        var mock = new MockDeviceFlowAuth()
            .WithDeviceCodeResponse(deviceCode)
            .WithPollResult(pollResult);

        var result = await mock.PollForTokenAsync(deviceCode);

        mock.PollWasCalled.ShouldBeTrue();
        result.Success.ShouldBeTrue();
        result.AccessToken.ShouldBe("token123");
    }

    [Fact]
    public async Task PollForTokenAsync_CallsWaitingCallback()
    {
        var deviceCode = new DeviceCodeResponse
        {
            DeviceCode = "dev123",
            UserCode = "CALLBACK-TEST",
            VerificationUri = "https://callback.test"
        };
        var mock = new MockDeviceFlowAuth()
            .WithPollResult(new DeviceFlowResult { Success = true, AccessToken = "token" });

        string? receivedUserCode = null;
        string? receivedUri = null;

        await mock.PollForTokenAsync(deviceCode, (code, uri) =>
        {
            receivedUserCode = code;
            receivedUri = uri;
        });

        receivedUserCode.ShouldBe("CALLBACK-TEST");
        receivedUri.ShouldBe("https://callback.test");
    }

    [Fact]
    public async Task PollForTokenAsync_ReturnsErrorWhenNotConfigured()
    {
        var deviceCode = new DeviceCodeResponse
        {
            DeviceCode = "dev123",
            UserCode = "TEST",
            VerificationUri = "https://example.com"
        };
        var mock = new MockDeviceFlowAuth();

        var result = await mock.PollForTokenAsync(deviceCode);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }
}
