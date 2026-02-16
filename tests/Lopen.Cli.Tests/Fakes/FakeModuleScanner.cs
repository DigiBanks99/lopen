using Lopen.Core.Workflow;

namespace Lopen.Cli.Tests.Fakes;

internal sealed class FakeModuleScanner : IModuleScanner
{
    private readonly List<ModuleInfo> _modules = [];

    public void AddModule(string name, bool hasSpec = true)
    {
        _modules.Add(new ModuleInfo(name, $"docs/requirements/{name}/SPECIFICATION.md", hasSpec));
    }

    public IReadOnlyList<ModuleInfo> ScanModules() => _modules.ToList();
}
