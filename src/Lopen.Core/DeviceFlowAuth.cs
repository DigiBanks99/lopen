using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lopen.Core;

/// <summary>
/// OAuth app configuration.
/// </summary>
public record OAuthAppConfig
{
    [JsonPropertyName("client-id")]
    public required string ClientId { get; init; }

    [JsonPropertyName("client-secret")]
    public string? ClientSecret { get; init; }

    [JsonPropertyName("redirect-uris")]
    public List<string>? RedirectUris { get; init; }
}

/// <summary>
/// Response from GitHub device code request.
/// </summary>
public record DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public required string DeviceCode { get; init; }

    [JsonPropertyName("user_code")]
    public required string UserCode { get; init; }

    [JsonPropertyName("verification_uri")]
    public required string VerificationUri { get; init; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    [JsonPropertyName("interval")]
    public int Interval { get; init; }
}

/// <summary>
/// Response from GitHub token request.
/// </summary>
public record TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }
}

/// <summary>
/// Result of device flow authentication.
/// </summary>
public record DeviceFlowResult
{
    public bool Success { get; init; }
    public string? AccessToken { get; init; }
    public string? Error { get; init; }
    public string? UserCode { get; init; }
    public string? VerificationUri { get; init; }
}

/// <summary>
/// Interface for OAuth2 device flow authentication.
/// </summary>
public interface IDeviceFlowAuth
{
    /// <summary>
    /// Gets the OAuth app configuration.
    /// </summary>
    OAuthAppConfig? GetConfig();

    /// <summary>
    /// Starts the device flow authentication process.
    /// </summary>
    Task<DeviceCodeResponse?> StartDeviceFlowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls for the access token after user authorizes.
    /// </summary>
    Task<DeviceFlowResult> PollForTokenAsync(
        DeviceCodeResponse deviceCode,
        Action<string, string>? onWaitingForUser = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// OAuth2 device flow authentication for GitHub.
/// </summary>
public class DeviceFlowAuth : IDeviceFlowAuth
{
    private readonly string _configPath;
    private readonly HttpClient _httpClient;
    private OAuthAppConfig? _config;

    private const string DeviceCodeEndpoint = "https://github.com/login/device/code";
    private const string TokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string DefaultScope = "copilot read:user";

    public DeviceFlowAuth() : this(GetDefaultConfigPath(), new HttpClient())
    {
    }

    public DeviceFlowAuth(string configPath) : this(configPath, new HttpClient())
    {
    }

    public DeviceFlowAuth(string configPath, HttpClient httpClient)
    {
        _configPath = configPath ?? throw new ArgumentNullException(nameof(configPath));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private static string GetDefaultConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "lopen", "oauth.json");
    }

    public OAuthAppConfig? GetConfig()
    {
        if (_config is not null)
            return _config;

        if (!File.Exists(_configPath))
            return null;

        try
        {
            var json = File.ReadAllText(_configPath);
            _config = JsonSerializer.Deserialize<OAuthAppConfig>(json);
            return _config;
        }
        catch
        {
            return null;
        }
    }

    public async Task<DeviceCodeResponse?> StartDeviceFlowAsync(CancellationToken cancellationToken = default)
    {
        var config = GetConfig();
        if (config is null)
            return null;

        var request = new HttpRequestMessage(HttpMethod.Post, DeviceCodeEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = config.ClientId,
                ["scope"] = DefaultScope
            })
        };
        request.Headers.Accept.Add(new("application/json"));

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<DeviceCodeResponse>(cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task<DeviceFlowResult> PollForTokenAsync(
        DeviceCodeResponse deviceCode,
        Action<string, string>? onWaitingForUser = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceCode);

        var config = GetConfig();
        if (config is null)
            return new DeviceFlowResult { Success = false, Error = "OAuth configuration not found" };

        onWaitingForUser?.Invoke(deviceCode.UserCode, deviceCode.VerificationUri);

        var interval = deviceCode.Interval > 0 ? deviceCode.Interval : 5;
        var expiresAt = DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        while (DateTime.UtcNow < expiresAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = config.ClientId,
                    ["device_code"] = deviceCode.DeviceCode,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
                })
            };
            request.Headers.Accept.Add(new("application/json"));

            try
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

                if (tokenResponse?.AccessToken is not null)
                {
                    return new DeviceFlowResult
                    {
                        Success = true,
                        AccessToken = tokenResponse.AccessToken,
                        UserCode = deviceCode.UserCode,
                        VerificationUri = deviceCode.VerificationUri
                    };
                }

                if (tokenResponse?.Error is not null)
                {
                    switch (tokenResponse.Error)
                    {
                        case "authorization_pending":
                            // Continue polling
                            break;
                        case "slow_down":
                            interval += 5;
                            break;
                        case "expired_token":
                            return new DeviceFlowResult
                            {
                                Success = false,
                                Error = "Device code expired. Please try again.",
                                UserCode = deviceCode.UserCode,
                                VerificationUri = deviceCode.VerificationUri
                            };
                        case "access_denied":
                            return new DeviceFlowResult
                            {
                                Success = false,
                                Error = "Authorization denied by user.",
                                UserCode = deviceCode.UserCode,
                                VerificationUri = deviceCode.VerificationUri
                            };
                        default:
                            return new DeviceFlowResult
                            {
                                Success = false,
                                Error = tokenResponse.ErrorDescription ?? tokenResponse.Error,
                                UserCode = deviceCode.UserCode,
                                VerificationUri = deviceCode.VerificationUri
                            };
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Continue polling on network errors
            }
        }

        return new DeviceFlowResult
        {
            Success = false,
            Error = "Device code expired. Please try again.",
            UserCode = deviceCode.UserCode,
            VerificationUri = deviceCode.VerificationUri
        };
    }
}

/// <summary>
/// Mock device flow auth for testing.
/// </summary>
public class MockDeviceFlowAuth : IDeviceFlowAuth
{
    private OAuthAppConfig? _config;
    private DeviceCodeResponse? _deviceCodeResponse;
    private DeviceFlowResult? _pollResult;
    private bool _startFlowShouldFail;

    public bool StartFlowWasCalled { get; private set; }
    public bool PollWasCalled { get; private set; }

    public MockDeviceFlowAuth WithConfig(OAuthAppConfig config)
    {
        _config = config;
        return this;
    }

    public MockDeviceFlowAuth WithDeviceCodeResponse(DeviceCodeResponse response)
    {
        _deviceCodeResponse = response;
        return this;
    }

    public MockDeviceFlowAuth WithPollResult(DeviceFlowResult result)
    {
        _pollResult = result;
        return this;
    }

    public MockDeviceFlowAuth ShouldFailToStart()
    {
        _startFlowShouldFail = true;
        return this;
    }

    public OAuthAppConfig? GetConfig() => _config;

    public Task<DeviceCodeResponse?> StartDeviceFlowAsync(CancellationToken cancellationToken = default)
    {
        StartFlowWasCalled = true;
        if (_startFlowShouldFail)
            return Task.FromResult<DeviceCodeResponse?>(null);
        return Task.FromResult(_deviceCodeResponse);
    }

    public Task<DeviceFlowResult> PollForTokenAsync(
        DeviceCodeResponse deviceCode,
        Action<string, string>? onWaitingForUser = null,
        CancellationToken cancellationToken = default)
    {
        PollWasCalled = true;
        onWaitingForUser?.Invoke(deviceCode.UserCode, deviceCode.VerificationUri);
        return Task.FromResult(_pollResult ?? new DeviceFlowResult { Success = false, Error = "Not configured" });
    }
}
