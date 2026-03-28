using System.ComponentModel;
using Lopen.Storage;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tools;

/// <summary>
/// Static operations for specification-related tools.
/// </summary>
internal static class SpecificationOperations
{
    public static void AddTools(List<AIFunction> tools, IFileSystem fileSystem, IToolSectionExtractor sectionExtractor, string projectRoot)
    {
        tools.Add(AIFunctionFactory.Create(
            [Description("Read a specific section from a specification document")]
            (string module, string? section) => ReadSpec(fileSystem, sectionExtractor, projectRoot, module, section),
            "read_spec"));
    }

    internal static async Task<string> ReadSpec(
        IFileSystem fileSystem,
        IToolSectionExtractor sectionExtractor,
        string projectRoot,
        string module,
        string? section)
    {
        var specPath = Path.Combine(projectRoot, "docs", "requirements", module, "SPECIFICATION.md");
        if (!fileSystem.FileExists(specPath))
            return JsonResult("error", $"Specification not found for module '{module}'");

        var content = await fileSystem.ReadAllTextAsync(specPath);

        if (!string.IsNullOrWhiteSpace(section))
        {
            IReadOnlyList<ToolExtractedSection> sections = sectionExtractor.ExtractRelevantSections(content, [section]);
            if (sections.Count > 0)
                return string.Join("\n\n", sections.Select(s => s.Content));
            return JsonResult("error", $"Section '{section}' not found in specification");
        }

        return content;
    }

    internal static string JsonResult(string status, string message) =>
        System.Text.Json.JsonSerializer.Serialize(new { status, message });
}
