using Lopen.Core.Documents;
using Lopen.Storage;
using Microsoft.Extensions.Logging;

namespace Lopen.Core.Workflow;

/// <summary>
/// Lists modules with their current state by scanning specs and counting
/// acceptance criteria checkboxes.
/// </summary>
internal sealed class ModuleLister : IModuleLister
{
    private readonly IModuleScanner _scanner;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<ModuleLister> _logger;

    public ModuleLister(
        IModuleScanner scanner,
        IFileSystem fileSystem,
        ILogger<ModuleLister> logger)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ModuleState> ListModules()
    {
        var modules = _scanner.ScanModules();
        var result = new List<ModuleState>(modules.Count);

        foreach (var module in modules)
        {
            var state = DetermineState(module);
            result.Add(state);
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result.AsReadOnly();
    }

    private ModuleState DetermineState(ModuleInfo module)
    {
        if (!module.HasSpecification)
        {
            return new ModuleState(module.Name, module.SpecificationPath, ModuleStatus.Unknown, 0, 0);
        }

        try
        {
            var content = _fileSystem.ReadAllTextAsync(module.SpecificationPath).GetAwaiter().GetResult();
            var (total, completed) = MarkdownUpdater.CountCheckboxes(content);

            var status = total == 0
                ? ModuleStatus.NotStarted
                : completed == total
                    ? ModuleStatus.Complete
                    : completed == 0
                        ? ModuleStatus.NotStarted
                        : ModuleStatus.InProgress;

            return new ModuleState(module.Name, module.SpecificationPath, status, completed, total);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read spec for module {Module}", module.Name);
            return new ModuleState(module.Name, module.SpecificationPath, ModuleStatus.Unknown, 0, 0);
        }
    }
}
