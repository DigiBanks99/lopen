namespace Lopen.Core;

/// <summary>
/// Mock credential store for testing.
/// Stores tokens in-memory with tracking of operations.
/// </summary>
public class MockCredentialStore : ICredentialStore, ITokenInfoStore
{
    private string? _token;
    private TokenInfo? _tokenInfo;
    private readonly List<string> _operationLog = [];

    /// <summary>
    /// The currently stored token.
    /// </summary>
    public string? CurrentToken => _token;

    /// <summary>
    /// The currently stored token info.
    /// </summary>
    public TokenInfo? CurrentTokenInfo => _tokenInfo;

    /// <summary>
    /// Log of operations performed on this store.
    /// </summary>
    public IReadOnlyList<string> OperationLog => _operationLog.AsReadOnly();

    /// <summary>
    /// Number of times GetTokenAsync was called.
    /// </summary>
    public int GetTokenCallCount { get; private set; }

    /// <summary>
    /// Number of times GetTokenInfoAsync was called.
    /// </summary>
    public int GetTokenInfoCallCount { get; private set; }

    /// <summary>
    /// Number of times StoreTokenAsync was called.
    /// </summary>
    public int StoreTokenCallCount { get; private set; }

    /// <summary>
    /// Number of times StoreTokenInfoAsync was called.
    /// </summary>
    public int StoreTokenInfoCallCount { get; private set; }

    /// <summary>
    /// Number of times ClearAsync was called.
    /// </summary>
    public int ClearCallCount { get; private set; }

    /// <summary>
    /// If set, GetTokenAsync will throw this exception.
    /// </summary>
    public Exception? GetTokenException { get; set; }

    /// <summary>
    /// If set, StoreTokenAsync will throw this exception.
    /// </summary>
    public Exception? StoreTokenException { get; set; }

    /// <summary>
    /// Pre-seed the store with a token.
    /// </summary>
    public MockCredentialStore WithToken(string token)
    {
        _token = token;
        return this;
    }

    /// <summary>
    /// Pre-seed the store with token info.
    /// </summary>
    public MockCredentialStore WithTokenInfo(TokenInfo tokenInfo)
    {
        _tokenInfo = tokenInfo;
        _token = tokenInfo.AccessToken;
        return this;
    }

    public Task<string?> GetTokenAsync()
    {
        GetTokenCallCount++;
        _operationLog.Add($"GetToken");

        if (GetTokenException is not null)
            throw GetTokenException;

        return Task.FromResult(_token);
    }

    public Task<TokenInfo?> GetTokenInfoAsync()
    {
        GetTokenInfoCallCount++;
        _operationLog.Add($"GetTokenInfo");

        if (GetTokenException is not null)
            throw GetTokenException;

        return Task.FromResult(_tokenInfo);
    }

    public Task StoreTokenAsync(string token)
    {
        StoreTokenCallCount++;
        _operationLog.Add($"StoreToken: {token}");

        if (StoreTokenException is not null)
            throw StoreTokenException;

        _token = token;
        return Task.CompletedTask;
    }

    public Task StoreTokenInfoAsync(TokenInfo tokenInfo)
    {
        StoreTokenInfoCallCount++;
        _operationLog.Add($"StoreTokenInfo: {tokenInfo.AccessToken}");

        if (StoreTokenException is not null)
            throw StoreTokenException;

        _tokenInfo = tokenInfo;
        _token = tokenInfo.AccessToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        ClearCallCount++;
        _operationLog.Add("Clear");
        _token = null;
        _tokenInfo = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Resets all tracking counters and logs.
    /// </summary>
    public void Reset()
    {
        GetTokenCallCount = 0;
        GetTokenInfoCallCount = 0;
        StoreTokenCallCount = 0;
        StoreTokenInfoCallCount = 0;
        ClearCallCount = 0;
        _operationLog.Clear();
    }
}
