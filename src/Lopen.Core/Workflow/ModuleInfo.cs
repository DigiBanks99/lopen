namespace Lopen.Core.Workflow;

/// <summary>
/// Represents a discovered module specification.
/// </summary>
/// <param name="Name">Module name (directory name under docs/requirements/).</param>
/// <param name="SpecificationPath">Full path to the SPECIFICATION.md file.</param>
/// <param name="HasSpecification">Whether a SPECIFICATION.md file exists.</param>
public sealed record ModuleInfo(string Name, string SpecificationPath, bool HasSpecification);
