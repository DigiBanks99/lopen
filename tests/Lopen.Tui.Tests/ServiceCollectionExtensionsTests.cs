using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lopen.Tui.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddLopenTui_RegistersWithoutError()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var exception = Record.Exception(() => services.AddLopenTui());

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenTui_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var result = services.AddLopenTui();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddLopenTui_ServiceProviderBuildsWithoutError()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        var exception = Record.Exception(() =>
        {
            using var provider = services.BuildServiceProvider();
        });

        Assert.Null(exception);
    }

    [Fact]
    public void AddLopenTui_ResolvesTuiApplication()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var app = provider.GetService<ITuiApplication>();

        Assert.NotNull(app);
    }

    [Fact]
    public void AddLopenTui_ResolvesComponentGallery()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var gallery = provider.GetService<IComponentGallery>();

        Assert.NotNull(gallery);
    }

    [Fact]
    public void AddLopenTui_TuiApplication_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var app1 = provider.GetRequiredService<ITuiApplication>();
        var app2 = provider.GetRequiredService<ITuiApplication>();

        Assert.Same(app1, app2);
    }

    [Fact]
    public void AddLopenTui_ComponentGallery_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLopenTui();

        using var provider = services.BuildServiceProvider();
        var gallery1 = provider.GetRequiredService<IComponentGallery>();
        var gallery2 = provider.GetRequiredService<IComponentGallery>();

        Assert.Same(gallery1, gallery2);
    }

    [Fact]
    public void AddUserPromptQueue_RegistersQueue()
    {
        var services = new ServiceCollection();
        services.AddUserPromptQueue();

        using var provider = services.BuildServiceProvider();
        var queue = provider.GetService<IUserPromptQueue>();

        Assert.NotNull(queue);
        Assert.IsType<UserPromptQueue>(queue);
    }

    [Fact]
    public void AddUserPromptQueue_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddUserPromptQueue();

        using var provider = services.BuildServiceProvider();
        var queue1 = provider.GetRequiredService<IUserPromptQueue>();
        var queue2 = provider.GetRequiredService<IUserPromptQueue>();

        Assert.Same(queue1, queue2);
    }

    [Fact]
    public void AddSessionDetector_RegistersDetector()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Lopen.Storage.ISessionManager, StubSessionManager>();
        services.AddSessionDetector();

        using var provider = services.BuildServiceProvider();
        var detector = provider.GetService<ISessionDetector>();

        Assert.NotNull(detector);
        Assert.IsType<SessionDetector>(detector);
    }

    [Fact]
    public void AddSessionDetector_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Lopen.Storage.ISessionManager, StubSessionManager>();
        services.AddSessionDetector();

        using var provider = services.BuildServiceProvider();
        var d1 = provider.GetRequiredService<ISessionDetector>();
        var d2 = provider.GetRequiredService<ISessionDetector>();

        Assert.Same(d1, d2);
    }

    private sealed class StubSessionManager : Lopen.Storage.ISessionManager
    {
        public Task<Lopen.Storage.SessionId?> GetLatestSessionIdAsync(CancellationToken ct = default) => Task.FromResult<Lopen.Storage.SessionId?>(null);
        public Task<Lopen.Storage.SessionState?> LoadSessionStateAsync(Lopen.Storage.SessionId s, CancellationToken ct = default) => Task.FromResult<Lopen.Storage.SessionState?>(null);
        public Task<Lopen.Storage.SessionId> CreateSessionAsync(string m, CancellationToken ct = default) => Task.FromResult(Lopen.Storage.SessionId.Generate(m, DateOnly.FromDateTime(DateTime.UtcNow), 1));
        public Task SaveSessionStateAsync(Lopen.Storage.SessionId s, Lopen.Storage.SessionState st, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Lopen.Storage.SessionMetrics?> LoadSessionMetricsAsync(Lopen.Storage.SessionId s, CancellationToken ct = default) => Task.FromResult<Lopen.Storage.SessionMetrics?>(null);
        public Task SaveSessionMetricsAsync(Lopen.Storage.SessionId s, Lopen.Storage.SessionMetrics m, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Lopen.Storage.SessionId>> ListSessionsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Lopen.Storage.SessionId>>([]);
        public Task SetLatestAsync(Lopen.Storage.SessionId s, CancellationToken ct = default) => Task.CompletedTask;
        public Task QuarantineCorruptedSessionAsync(Lopen.Storage.SessionId s, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> PruneSessionsAsync(int m = 30, CancellationToken ct = default) => Task.FromResult(0);
        public Task DeleteSessionAsync(Lopen.Storage.SessionId s, CancellationToken ct = default) => Task.CompletedTask;
    }
}
