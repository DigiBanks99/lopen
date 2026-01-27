using Shouldly;
using Xunit;

namespace Lopen.Core.Tests;

public class SecureCredentialStoreTests
{
    [Fact]
    public void IsAvailable_ReturnsBoolean_WithoutThrowing()
    {
        // This may return true or false depending on platform/configuration
        // The key is that it should never throw
        var result = SecureCredentialStore.IsAvailable();
        
        // Just verify it doesn't throw and returns a valid bool
        result.ShouldBeOneOf(true, false);
    }

    [Fact]
    public void IsAvailable_CanBeCalledMultipleTimes()
    {
        // Verify IsAvailable is idempotent and doesn't have side effects
        var result1 = SecureCredentialStore.IsAvailable();
        var result2 = SecureCredentialStore.IsAvailable();
        
        result1.ShouldBe(result2);
    }

    [Fact]
    public void Constructor_ThrowsHelpfulException_WhenStoreFails()
    {
        // Skip on platforms where secure storage works
        if (SecureCredentialStore.IsAvailable())
        {
            return; // Can't test failure case when it works
        }

        // On platforms where IsAvailable() returns false, constructor should
        // throw a helpful exception if somehow called anyway
        var ex = Should.Throw<InvalidOperationException>(() => new SecureCredentialStore());
        
        ex.Message.ShouldContain("credential store");
    }

    [Fact]
    public async Task StoreTokenAsync_WithEmptyToken_ThrowsArgumentException()
    {
        var store = new MockGcmCredentialStore();
        var secureStore = CreateSecureStoreWithMock(store);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await secureStore.StoreTokenAsync(""));
    }

    [Fact]
    public async Task StoreTokenAsync_WithWhitespaceToken_ThrowsArgumentException()
    {
        var store = new MockGcmCredentialStore();
        var secureStore = CreateSecureStoreWithMock(store);

        await Should.ThrowAsync<ArgumentException>(async () =>
            await secureStore.StoreTokenAsync("   "));
    }

    [Fact]
    public async Task StoreTokenAsync_StoresToken_InUnderlyingStore()
    {
        var store = new MockGcmCredentialStore();
        var secureStore = CreateSecureStoreWithMock(store);

        await secureStore.StoreTokenAsync("test-token");

        store.AddOrUpdateWasCalled.ShouldBeTrue();
        store.LastStoredPassword.ShouldBe("test-token");
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenNoCredential()
    {
        var store = new MockGcmCredentialStore { ReturnCredential = null };
        var secureStore = CreateSecureStoreWithMock(store);

        var token = await secureStore.GetTokenAsync();

        token.ShouldBeNull();
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsPassword_WhenCredentialExists()
    {
        var store = new MockGcmCredentialStore 
        { 
            ReturnCredential = new MockCredential("account", "stored-password") 
        };
        var secureStore = CreateSecureStoreWithMock(store);

        var token = await secureStore.GetTokenAsync();

        token.ShouldBe("stored-password");
    }

    [Fact]
    public async Task GetTokenAsync_ReturnsNull_WhenStoreThrows()
    {
        var store = new MockGcmCredentialStore 
        { 
            GetThrows = new InvalidOperationException("Store error") 
        };
        var secureStore = CreateSecureStoreWithMock(store);

        var token = await secureStore.GetTokenAsync();

        token.ShouldBeNull();
    }

    [Fact]
    public async Task ClearAsync_RemovesCredential()
    {
        var store = new MockGcmCredentialStore();
        var secureStore = CreateSecureStoreWithMock(store);

        await secureStore.ClearAsync();

        store.RemoveWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ClearAsync_DoesNotThrow_WhenCredentialDoesNotExist()
    {
        var store = new MockGcmCredentialStore 
        { 
            RemoveThrows = new InvalidOperationException("Not found") 
        };
        var secureStore = CreateSecureStoreWithMock(store);

        // Should not throw
        await secureStore.ClearAsync();
    }

    [Fact]
    public async Task StoreTokenAsync_ThrowsInvalidOperationException_WhenStoreFails()
    {
        var store = new MockGcmCredentialStore 
        { 
            AddOrUpdateThrows = new Exception("Storage failure") 
        };
        var secureStore = CreateSecureStoreWithMock(store);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await secureStore.StoreTokenAsync("token"));
    }

    private static SecureCredentialStore CreateSecureStoreWithMock(MockGcmCredentialStore mock)
    {
        // Use reflection to access internal constructor
        var ctor = typeof(SecureCredentialStore).GetConstructor(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
            null,
            [typeof(GitCredentialManager.ICredentialStore)],
            null);
        return (SecureCredentialStore)ctor!.Invoke([mock]);
    }

    /// <summary>
    /// Mock implementation of GitCredentialManager.ICredentialStore for testing.
    /// </summary>
    private class MockGcmCredentialStore : GitCredentialManager.ICredentialStore
    {
        public MockCredential? ReturnCredential { get; set; }
        public Exception? GetThrows { get; set; }
        public Exception? AddOrUpdateThrows { get; set; }
        public Exception? RemoveThrows { get; set; }
        public bool AddOrUpdateWasCalled { get; private set; }
        public bool RemoveWasCalled { get; private set; }
        public string? LastStoredPassword { get; private set; }

        public GitCredentialManager.ICredential? Get(string service, string? account)
        {
            if (GetThrows is not null) throw GetThrows;
            return ReturnCredential;
        }

        public void AddOrUpdate(string service, string account, string secret)
        {
            if (AddOrUpdateThrows is not null) throw AddOrUpdateThrows;
            AddOrUpdateWasCalled = true;
            LastStoredPassword = secret;
        }

        public bool Remove(string service, string account)
        {
            if (RemoveThrows is not null) throw RemoveThrows;
            RemoveWasCalled = true;
            return true;
        }

        public IEnumerable<GitCredentialManager.ICredential> GetAll(string service)
        {
            if (ReturnCredential is not null)
                return [ReturnCredential];
            return [];
        }

        public IList<string> GetAccounts(string service)
        {
            if (ReturnCredential is not null)
                return [ReturnCredential.Account];
            return [];
        }
    }

    private record MockCredential(string Account, string Password) : GitCredentialManager.ICredential;
}

public class MockCredentialStoreTests
{
    [Fact]
    public async Task WithToken_SeedsInitialToken()
    {
        var store = new MockCredentialStore().WithToken("initial-token");

        var token = await store.GetTokenAsync();

        token.ShouldBe("initial-token");
    }

    [Fact]
    public async Task StoreTokenAsync_StoresAndReturnsToken()
    {
        var store = new MockCredentialStore();

        await store.StoreTokenAsync("my-token");
        var token = await store.GetTokenAsync();

        token.ShouldBe("my-token");
        store.CurrentToken.ShouldBe("my-token");
    }

    [Fact]
    public async Task ClearAsync_ClearsToken()
    {
        var store = new MockCredentialStore().WithToken("token");

        await store.ClearAsync();

        store.CurrentToken.ShouldBeNull();
    }

    [Fact]
    public async Task OperationCounts_TracksCorrectly()
    {
        var store = new MockCredentialStore();

        await store.GetTokenAsync();
        await store.GetTokenAsync();
        await store.StoreTokenAsync("token");
        await store.ClearAsync();

        store.GetTokenCallCount.ShouldBe(2);
        store.StoreTokenCallCount.ShouldBe(1);
        store.ClearCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task OperationLog_TracksOperations()
    {
        var store = new MockCredentialStore();

        await store.GetTokenAsync();
        await store.StoreTokenAsync("test-token");
        await store.ClearAsync();

        store.OperationLog.ShouldBe(new[] { "GetToken", "StoreToken: test-token", "Clear" });
    }

    [Fact]
    public void Reset_ClearsCountersAndLogs()
    {
        var store = new MockCredentialStore().WithToken("token");
        store.GetTokenAsync();
        store.StoreTokenAsync("new");

        store.Reset();

        store.GetTokenCallCount.ShouldBe(0);
        store.StoreTokenCallCount.ShouldBe(0);
        store.ClearCallCount.ShouldBe(0);
        store.OperationLog.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTokenException_ThrowsOnGet()
    {
        var store = new MockCredentialStore
        {
            GetTokenException = new InvalidOperationException("Test error")
        };

        await Should.ThrowAsync<InvalidOperationException>(() => store.GetTokenAsync());
    }

    [Fact]
    public async Task StoreTokenException_ThrowsOnStore()
    {
        var store = new MockCredentialStore
        {
            StoreTokenException = new InvalidOperationException("Test error")
        };

        await Should.ThrowAsync<InvalidOperationException>(() => store.StoreTokenAsync("token"));
    }
}

public class CredentialMigrationTests
{
    [Fact]
    public async Task MigrateIfNeededAsync_DoesNothing_WhenSecureStoreHasToken()
    {
        var secureStore = new MockCredentialStore().WithToken("existing");
        var fileStore = CreateFileStore("old-token");

        var migrated = await CredentialMigration.MigrateIfNeededAsync(secureStore, fileStore);

        migrated.ShouldBeFalse();
        secureStore.CurrentToken.ShouldBe("existing");
    }

    [Fact]
    public async Task MigrateIfNeededAsync_DoesNothing_WhenNoTokenExists()
    {
        var secureStore = new MockCredentialStore();
        var fileStore = CreateFileStore(null);

        var migrated = await CredentialMigration.MigrateIfNeededAsync(secureStore, fileStore);

        migrated.ShouldBeFalse();
    }

    [Fact]
    public async Task MigrateIfNeededAsync_MigratesToken_WhenOnlyFileStoreHasToken()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}", "credentials.json");
        Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
        
        try
        {
            var secureStore = new MockCredentialStore();
            var fileStore = new FileCredentialStore(tempPath);
            await fileStore.StoreTokenAsync("file-token");

            var migrated = await CredentialMigration.MigrateIfNeededAsync(secureStore, fileStore);

            migrated.ShouldBeTrue();
            secureStore.CurrentToken.ShouldBe("file-token");
            
            // Verify file store was cleared
            var remainingToken = await fileStore.GetTokenAsync();
            remainingToken.ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(tempPath)))
                Directory.Delete(Path.GetDirectoryName(tempPath)!, true);
        }
    }

    private static FileCredentialStore CreateFileStore(string? token)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"lopen-test-{Guid.NewGuid()}", "credentials.json");
        var store = new FileCredentialStore(tempPath);
        if (token is not null)
        {
            store.StoreTokenAsync(token).Wait();
        }
        return store;
    }
}

public class CredentialStoreFactoryTests
{
    [Fact]
    public void Create_ReturnsCredentialStore()
    {
        var factory = new CredentialStoreFactory();

        var store = factory.Create();

        store.ShouldNotBeNull();
        // Should be either SecureCredentialStore or FileCredentialStore depending on platform
        store.ShouldBeAssignableTo<ICredentialStore>();
    }

    [Fact]
    public void Create_ReturnsFileStore_WhenSecureNotAvailable()
    {
        var factory = new CredentialStoreFactory();

        var store = factory.Create();

        // If secure storage is not available, should get FileCredentialStore
        if (!SecureCredentialStore.IsAvailable())
        {
            store.ShouldBeOfType<FileCredentialStore>();
        }
    }

    [Fact]
    public void Create_ReturnsSecureStore_WhenSecureAvailable()
    {
        var factory = new CredentialStoreFactory();

        var store = factory.Create();

        // If secure storage is available, should get SecureCredentialStore
        if (SecureCredentialStore.IsAvailable())
        {
            store.ShouldBeOfType<SecureCredentialStore>();
        }
    }
}
