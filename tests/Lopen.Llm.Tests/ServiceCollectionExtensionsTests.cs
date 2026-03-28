using Lopen.Auth;
using Lopen.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Lopen.Llm.Tests;

public class ServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var lopenOptions = new LopenOptions();
        services.AddSingleton(Options.Create(lopenOptions));
        services.AddSingleton(lopenOptions.Oracle);
        services.AddSingleton<IAuthTokenProvider>(new TestTokenProvider("test-token"));
        services.AddLopenLlm();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AddLopenLlm_RegistersILlmService()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            ILlmService? service = provider.GetService<ILlmService>();

            Assert.NotNull(service);
            Assert.IsType<RetryingLlmService>(service);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIModelSelector()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IModelSelector? selector = provider.GetService<IModelSelector>();

            Assert.NotNull(selector);
            Assert.IsType<DefaultModelSelector>(selector);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersITokenTracker()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            ITokenTracker? tracker = provider.GetService<ITokenTracker>();

            Assert.NotNull(tracker);
            Assert.IsType<InMemoryTokenTracker>(tracker);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public void AddLopenLlm_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        IServiceCollection result = services.AddLopenLlm();

        Assert.Same(services, result);
    }

    [Fact]
    public async Task AddLopenLlm_ILlmService_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            ILlmService first = provider.GetRequiredService<ILlmService>();
            ILlmService second = provider.GetRequiredService<ILlmService>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_IModelSelector_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IModelSelector first = provider.GetRequiredService<IModelSelector>();
            IModelSelector second = provider.GetRequiredService<IModelSelector>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_ITokenTracker_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            ITokenTracker first = provider.GetRequiredService<ITokenTracker>();
            ITokenTracker second = provider.GetRequiredService<ITokenTracker>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIPromptBuilder()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IPromptBuilder? builder = provider.GetService<IPromptBuilder>();

            Assert.NotNull(builder);
            Assert.IsType<DefaultPromptBuilder>(builder);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIVerificationTracker()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IVerificationTracker? tracker = provider.GetService<IVerificationTracker>();

            Assert.NotNull(tracker);
            Assert.IsType<VerificationTracker>(tracker);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersIOracleVerifier()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IOracleVerifier? verifier = provider.GetService<IOracleVerifier>();

            Assert.NotNull(verifier);
            Assert.IsType<OracleVerifier>(verifier);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_IOracleVerifier_IsSingleton()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IOracleVerifier first = provider.GetRequiredService<IOracleVerifier>();
            IOracleVerifier second = provider.GetRequiredService<IOracleVerifier>();

            Assert.Same(first, second);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RequiresCallerProvidedIAuthTokenProvider()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            IAuthTokenProvider tokenProvider = provider.GetRequiredService<IAuthTokenProvider>();

            Assert.IsType<TestTokenProvider>(tokenProvider);
            Assert.Equal("test-token", await tokenProvider.GetTokenAsync());
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_RegistersICopilotClientProvider()
    {
        ServiceProvider provider = BuildProvider();
        try
        {
            ICopilotClientProvider? clientProvider = provider.GetService<ICopilotClientProvider>();

            Assert.NotNull(clientProvider);
            Assert.IsType<CopilotClientProvider>(clientProvider);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    [Fact]
    public async Task AddLopenLlm_CopilotClientProvider_ResolvesWithProvidedAuthTokenProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var lopenOptions = new LopenOptions();
        services.AddSingleton(Options.Create(lopenOptions));
        services.AddSingleton(lopenOptions.Oracle);
        services.AddSingleton<IAuthTokenProvider>(new TestTokenProvider("test-token"));
        services.AddLopenLlm();
        ServiceProvider provider = services.BuildServiceProvider();
        try
        {
            ICopilotClientProvider copilotClientProvider = provider.GetRequiredService<ICopilotClientProvider>();

            Assert.IsType<CopilotClientProvider>(copilotClientProvider);
        }
        finally
        {
            await provider.DisposeAsync();
        }
    }

    private sealed class TestTokenProvider(string? token) : IAuthTokenProvider
    {
        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(token);
    }
}
