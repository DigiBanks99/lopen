using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Lopen.Storage;
using Microsoft.Extensions.AI;

namespace Lopen.Llm.Tools;

/// <summary>
/// Static operations for research-related tools.
/// </summary>
internal static class ResearchOperations
{
    public static void AddTools(List<AIFunction> tools, IFileSystem fileSystem, string projectRoot)
    {
        tools.Add(AIFunctionFactory.Create(
            [Description("Read findings from a research document")]
            (string module, string? topic) => ReadResearch(fileSystem, projectRoot, module, topic),
            "read_research"));

        tools.Add(AIFunctionFactory.Create(
            [Description("Save research findings to docs/requirements/{module}/RESEARCH-{topic}.md")]
            (string module, string? topic, string content) => LogResearch(fileSystem, projectRoot, module, topic, content),
            "log_research"));
    }

    internal static async Task<string> ReadResearch(
        IFileSystem fileSystem,
        string projectRoot,
        string module,
        string? topic)
    {
        var researchDir = Path.Combine(projectRoot, "docs", "requirements", module);
        if (!fileSystem.DirectoryExists(researchDir))
            return JsonResult("error", $"Research directory not found for module '{module}'");

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var topicPath = Path.Combine(researchDir, $"RESEARCH-{topic}.md");
            if (fileSystem.FileExists(topicPath))
                return await fileSystem.ReadAllTextAsync(topicPath);

            return JsonResult("error", $"Research file not found: RESEARCH-{topic}.md");
        }

        var mainPath = Path.Combine(researchDir, "RESEARCH.md");
        if (fileSystem.FileExists(mainPath))
            return await fileSystem.ReadAllTextAsync(mainPath);

        return JsonResult("error", "No RESEARCH.md found");
    }

    internal static async Task<string> LogResearch(
        IFileSystem fileSystem,
        string projectRoot,
        string module,
        string? topic,
        string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return JsonResult("error", "content is required");

        var sanitizedTopic = SanitizeTopicSlug(topic ?? "general");

        var researchDir = Path.Combine(projectRoot, "docs", "requirements", module);
        fileSystem.CreateDirectory(researchDir);

        var filePath = Path.Combine(researchDir, $"RESEARCH-{sanitizedTopic}.md");
        await fileSystem.WriteAllTextAsync(filePath, content);

        await UpdateResearchIndexAsync(fileSystem, researchDir);

        return JsonResult("success", $"Research saved to RESEARCH-{sanitizedTopic}.md");
    }

    internal static string SanitizeTopicSlug(string topic)
    {
        var slug = Regex.Replace(topic.Trim(), @"[^a-zA-Z0-9\-_]", "-");
        slug = Regex.Replace(slug, @"-{2,}", "-");
        return slug.Trim('-');
    }

    internal static async Task UpdateResearchIndexAsync(IFileSystem fileSystem, string researchDir)
    {
        var files = fileSystem.GetFiles(researchDir, "RESEARCH-*.md")
            .Select(Path.GetFileName)
            .Where(f => f is not null)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("# Research Index");
        sb.AppendLine();
        foreach (var file in files)
        {
            var topicName = file!.Replace("RESEARCH-", "").Replace(".md", "");
            sb.AppendLine($"- [{topicName}]({file})");
        }

        var indexPath = Path.Combine(researchDir, "RESEARCH.md");
        await fileSystem.WriteAllTextAsync(indexPath, sb.ToString());
    }

    internal static string JsonResult(string status, string message) =>
        System.Text.Json.JsonSerializer.Serialize(new { status, message });
}
