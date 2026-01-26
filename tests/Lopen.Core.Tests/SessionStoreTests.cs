using Shouldly;

namespace Lopen.Core.Tests;

public class SessionStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileSessionStore _store;

    public SessionStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lopen-session-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _store = new FileSessionStore(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesSessionFile()
    {
        var session = new PersistableSessionState
        {
            SessionId = "test123",
            StartedAt = DateTimeOffset.UtcNow,
            SavedAt = DateTimeOffset.UtcNow,
            CommandCount = 5
        };

        await _store.SaveAsync(session);

        var filePath = Path.Combine(_testDirectory, "test123.json");
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullForMissingSession()
    {
        var result = await _store.LoadAsync("nonexistent");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_LoadsSavedSession()
    {
        var session = new PersistableSessionState
        {
            SessionId = "load-test",
            StartedAt = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero),
            SavedAt = DateTimeOffset.UtcNow,
            CommandCount = 10,
            ConversationHistory = ["hello", "world"],
            Preferences = new() { ["theme"] = "dark" },
            Name = "my-session"
        };

        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync("load-test");

        loaded.ShouldNotBeNull();
        loaded.SessionId.ShouldBe("load-test");
        loaded.CommandCount.ShouldBe(10);
        loaded.ConversationHistory.ShouldContain("hello");
        loaded.ConversationHistory.ShouldContain("world");
        loaded.Preferences["theme"].ShouldBe("dark");
        loaded.Name.ShouldBe("my-session");
    }

    [Fact]
    public async Task LoadAsync_FindsByName()
    {
        var session = new PersistableSessionState
        {
            SessionId = "id123",
            Name = "named-session",
            SavedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(session);
        var loaded = await _store.LoadAsync("named-session");

        loaded.ShouldNotBeNull();
        loaded.SessionId.ShouldBe("id123");
    }

    [Fact]
    public async Task DeleteAsync_RemovesSessionFile()
    {
        var session = new PersistableSessionState
        {
            SessionId = "to-delete",
            SavedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(session);
        var filePath = Path.Combine(_testDirectory, "to-delete.json");
        File.Exists(filePath).ShouldBeTrue();

        var deleted = await _store.DeleteAsync("to-delete");

        deleted.ShouldBeTrue();
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseForMissingSession()
    {
        var deleted = await _store.DeleteAsync("nonexistent");

        deleted.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_FindsByName()
    {
        var session = new PersistableSessionState
        {
            SessionId = "id456",
            Name = "delete-by-name",
            SavedAt = DateTimeOffset.UtcNow
        };

        await _store.SaveAsync(session);
        var deleted = await _store.DeleteAsync("delete-by-name");

        deleted.ShouldBeTrue();
        (await _store.ExistsAsync("id456")).ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyForEmptyDirectory()
    {
        var sessions = await _store.ListAsync();

        sessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_ReturnsAllSessions()
    {
        await _store.SaveAsync(new PersistableSessionState { SessionId = "s1", SavedAt = DateTimeOffset.UtcNow.AddMinutes(-2) });
        await _store.SaveAsync(new PersistableSessionState { SessionId = "s2", SavedAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        await _store.SaveAsync(new PersistableSessionState { SessionId = "s3", SavedAt = DateTimeOffset.UtcNow });

        var sessions = await _store.ListAsync();

        sessions.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ListAsync_ReturnsSortedByMostRecentFirst()
    {
        await _store.SaveAsync(new PersistableSessionState { SessionId = "oldest", SavedAt = DateTimeOffset.UtcNow.AddMinutes(-10) });
        await _store.SaveAsync(new PersistableSessionState { SessionId = "newest", SavedAt = DateTimeOffset.UtcNow });
        await _store.SaveAsync(new PersistableSessionState { SessionId = "middle", SavedAt = DateTimeOffset.UtcNow.AddMinutes(-5) });

        var sessions = await _store.ListAsync();

        sessions[0].SessionId.ShouldBe("newest");
        sessions[1].SessionId.ShouldBe("middle");
        sessions[2].SessionId.ShouldBe("oldest");
    }

    [Fact]
    public async Task ExistsAsync_ReturnsFalseForMissing()
    {
        var exists = await _store.ExistsAsync("missing");

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ReturnsTrueForExisting()
    {
        await _store.SaveAsync(new PersistableSessionState { SessionId = "exists", SavedAt = DateTimeOffset.UtcNow });

        var exists = await _store.ExistsAsync("exists");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_FindsByName()
    {
        await _store.SaveAsync(new PersistableSessionState { SessionId = "id789", Name = "find-me", SavedAt = DateTimeOffset.UtcNow });

        var exists = await _store.ExistsAsync("find-me");

        exists.ShouldBeTrue();
    }
}

public class PersistableSessionStateTests
{
    [Fact]
    public void FromSessionState_CreatesCorrectPersistable()
    {
        var state = new SessionState
        {
            CommandCount = 3
        };
        state.ConversationHistory.Add("entry1");
        state.Preferences["key"] = "value";

        var persistable = PersistableSessionState.FromSessionState(state, "test-name");

        persistable.SessionId.ShouldBe(state.SessionId);
        persistable.StartedAt.ShouldBe(state.StartedAt);
        persistable.CommandCount.ShouldBe(3);
        persistable.ConversationHistory.ShouldContain("entry1");
        persistable.Preferences["key"].ShouldBe("value");
        persistable.Name.ShouldBe("test-name");
    }

    [Fact]
    public void ToSessionState_RestoresCorrectly()
    {
        var persistable = new PersistableSessionState
        {
            SessionId = "abc123",
            StartedAt = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero),
            CommandCount = 7,
            ConversationHistory = ["msg1", "msg2"],
            Preferences = new() { ["pref"] = "val" }
        };

        var state = persistable.ToSessionState();

        state.SessionId.ShouldBe("abc123");
        state.StartedAt.ShouldBe(persistable.StartedAt);
        state.CommandCount.ShouldBe(7);
        state.ConversationHistory.Count.ShouldBe(2);
        state.Preferences["pref"].ShouldBe("val");
    }
}

public class MockSessionStoreTests
{
    [Fact]
    public async Task Save_TracksCall()
    {
        var store = new MockSessionStore();
        var session = new PersistableSessionState { SessionId = "test", SavedAt = DateTimeOffset.UtcNow };

        await store.SaveAsync(session);

        store.SaveWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task Load_TracksCall()
    {
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState { SessionId = "test", SavedAt = DateTimeOffset.UtcNow });

        await store.LoadAsync("test");

        store.LoadWasCalled.ShouldBeTrue();
        store.LastRequestedIdOrName.ShouldBe("test");
    }

    [Fact]
    public async Task Delete_TracksCall()
    {
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState { SessionId = "test", SavedAt = DateTimeOffset.UtcNow });

        await store.DeleteAsync("test");

        store.DeleteWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task List_TracksCall()
    {
        var store = new MockSessionStore();

        await store.ListAsync();

        store.ListWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task WithSession_AllowsPreloading()
    {
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState { SessionId = "preloaded", Name = "test", SavedAt = DateTimeOffset.UtcNow });

        var loaded = await store.LoadAsync("preloaded");

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("test");
    }

    [Fact]
    public async Task LoadByName_Works()
    {
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState { SessionId = "id123", Name = "by-name", SavedAt = DateTimeOffset.UtcNow });

        var loaded = await store.LoadAsync("by-name");

        loaded.ShouldNotBeNull();
        loaded.SessionId.ShouldBe("id123");
    }
}

public class SessionStateServicePersistenceTests
{
    [Fact]
    public async Task SaveSessionAsync_ThrowsWithoutStore()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);

        await Should.ThrowAsync<InvalidOperationException>(() => service.SaveSessionAsync());
    }

    [Fact]
    public async Task SaveSessionAsync_UsesStore()
    {
        var authService = new MockAuthService();
        var store = new MockSessionStore();
        var service = new SessionStateService(authService, store);

        await service.SaveSessionAsync("my-name");

        store.SaveWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadSessionAsync_ReturnsFalseForMissing()
    {
        var authService = new MockAuthService();
        var store = new MockSessionStore();
        var service = new SessionStateService(authService, store);

        var result = await service.LoadSessionAsync("nonexistent");

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task LoadSessionAsync_RestoresState()
    {
        var authService = new MockAuthService();
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState
            {
                SessionId = "restored",
                CommandCount = 15,
                SavedAt = DateTimeOffset.UtcNow
            });
        var service = new SessionStateService(authService, store);

        var result = await service.LoadSessionAsync("restored");

        result.ShouldBeTrue();
        service.CurrentState.SessionId.ShouldBe("restored");
        service.CurrentState.CommandCount.ShouldBe(15);
    }

    [Fact]
    public async Task DeleteSessionAsync_ThrowsWithoutStore()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);

        await Should.ThrowAsync<InvalidOperationException>(() => service.DeleteSessionAsync("test"));
    }

    [Fact]
    public async Task ListSessionsAsync_ThrowsWithoutStore()
    {
        var authService = new MockAuthService();
        var service = new SessionStateService(authService);

        await Should.ThrowAsync<InvalidOperationException>(() => service.ListSessionsAsync());
    }

    [Fact]
    public async Task ListSessionsAsync_ReturnsSessions()
    {
        var authService = new MockAuthService();
        var store = new MockSessionStore()
            .WithSession(new PersistableSessionState { SessionId = "s1", SavedAt = DateTimeOffset.UtcNow })
            .WithSession(new PersistableSessionState { SessionId = "s2", SavedAt = DateTimeOffset.UtcNow });
        var service = new SessionStateService(authService, store);

        var sessions = await service.ListSessionsAsync();

        sessions.Count.ShouldBe(2);
    }
}

/// <summary>
/// Mock auth service for testing.
/// </summary>
file class MockAuthService : IAuthService
{
    public bool IsAuthenticated { get; set; }
    public string? Token { get; set; }

    public Task<string?> GetTokenAsync() => Task.FromResult(Token);

    public Task<AuthStatus> GetStatusAsync() =>
        Task.FromResult(new AuthStatus(IsAuthenticated));

    public Task StoreTokenAsync(string token)
    {
        Token = token;
        IsAuthenticated = true;
        return Task.CompletedTask;
    }

    public Task StoreTokenInfoAsync(TokenInfo tokenInfo)
    {
        Token = tokenInfo.AccessToken;
        IsAuthenticated = true;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Token = null;
        IsAuthenticated = false;
        return Task.CompletedTask;
    }
}
