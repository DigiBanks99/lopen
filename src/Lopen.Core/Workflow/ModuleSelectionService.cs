using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Lists modules with current state and prompts user for selection via IOutputRenderer.
/// </summary>
internal sealed class ModuleSelectionService : IModuleSelectionService
{
    private readonly IModuleLister _moduleLister;
    private readonly IOutputRenderer _renderer;
    private readonly ILogger<ModuleSelectionService> _logger;

    public ModuleSelectionService(
        IModuleLister moduleLister,
        IOutputRenderer renderer,
        ILogger<ModuleSelectionService> logger)
    {
        _moduleLister = moduleLister ?? throw new ArgumentNullException(nameof(moduleLister));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> SelectModuleAsync(CancellationToken cancellationToken = default)
    {
        var modules = _moduleLister.ListModules();
        if (modules.Count == 0)
        {
            await _renderer.RenderErrorAsync("No modules found. Create a SPECIFICATION.md in docs/requirements/<module>/.");
            return null;
        }

        // Display module list with state
        var listing = FormatModuleList(modules);
        await _renderer.RenderResultAsync(listing);

        // Prompt for selection
        var response = await _renderer.PromptAsync(
            $"Select a module (1-{modules.Count})", cancellationToken);

        if (response is null)
        {
            _logger.LogDebug("Module selection cancelled (non-interactive mode)");
            return null;
        }

        if (int.TryParse(response.Trim(), out var index) && index >= 1 && index <= modules.Count)
        {
            var selected = modules[index - 1].Name;
            _logger.LogInformation("User selected module: {Module}", selected);
            return selected;
        }

        // Try matching by name
        var byName = modules.FirstOrDefault(m =>
            m.Name.Equals(response.Trim(), StringComparison.OrdinalIgnoreCase));
        if (byName is not null)
        {
            _logger.LogInformation("User selected module by name: {Module}", byName.Name);
            return byName.Name;
        }

        await _renderer.RenderErrorAsync($"Invalid selection: '{response}'. Expected a number (1-{modules.Count}) or module name.");
        return null;
    }

    internal static string FormatModuleList(IReadOnlyList<ModuleState> modules)
    {
        var lines = new List<string> { "Available modules:" };
        for (var i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            var status = m.Status switch
            {
                ModuleStatus.NotStarted => "○ Not Started",
                ModuleStatus.InProgress => $"◐ In Progress ({m.CompletedCriteria}/{m.TotalCriteria})",
                ModuleStatus.Complete => $"● Complete ({m.CompletedCriteria}/{m.TotalCriteria})",
                _ => "? Unknown"
            };
            lines.Add($"  {i + 1}. {m.Name,-20} {status}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
