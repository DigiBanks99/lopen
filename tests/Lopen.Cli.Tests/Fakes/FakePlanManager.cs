using Lopen.Storage;

namespace Lopen.Cli.Tests.Fakes;

internal sealed class FakePlanManager : IPlanManager
{
    private readonly Dictionary<string, string> _plans = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<PlanTask>> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public void AddPlan(string module, string content)
    {
        _plans[module] = content;
    }

    public Task WritePlanAsync(string module, string content, CancellationToken cancellationToken = default)
    {
        _plans[module] = content;
        return Task.CompletedTask;
    }

    public Task<string?> ReadPlanAsync(string module, CancellationToken cancellationToken = default)
    {
        _plans.TryGetValue(module, out var content);
        return Task.FromResult(content);
    }

    public Task<bool> PlanExistsAsync(string module, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_plans.ContainsKey(module));
    }

    public Task<bool> UpdateCheckboxAsync(string module, string taskText, bool completed, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<PlanTask>> ReadTasksAsync(string module, CancellationToken cancellationToken = default)
    {
        if (_tasks.TryGetValue(module, out var tasks))
            return Task.FromResult<IReadOnlyList<PlanTask>>(tasks);
        return Task.FromResult<IReadOnlyList<PlanTask>>(Array.Empty<PlanTask>());
    }
}
