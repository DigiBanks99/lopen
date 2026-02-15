# Research: Markdown Parsing for Section Extraction & Drift Detection

Research into C# (.NET 8+) approaches for parsing markdown documents, extracting sections by header path, hashing content for specification drift detection, and caching results.

**Context**: The core module requires parsing `SPECIFICATION.md` files to extract sections by header (e.g., `§ Authentication`, `## Acceptance Criteria`), hash section content for drift detection between iterations, and cache extracted sections keyed by file path + header + modification timestamp (see storage spec).

---

## 1. Markdig (Recommended Approach)

**Package**: [`Markdig`](https://github.com/xoofx/markdig) — most popular .NET markdown parser.
**Latest**: `0.45.0` | **License**: BSD-2-Clause | **Targets**: .NET 8+, .NET Standard 2.0/2.1, .NET Framework 4.6.2

### Key Types

| Type | Namespace | Role |
|---|---|---|
| `Markdown` | `Markdig` | Static entry — `Markdown.Parse()` |
| `MarkdownPipeline` | `Markdig` | Configured parser pipeline (immutable, thread-safe) |
| `MarkdownPipelineBuilder` | `Markdig` | Fluent builder for pipeline |
| `MarkdownDocument` | `Markdig.Syntax` | Root AST node (extends `ContainerBlock`) |
| `HeadingBlock` | `Markdig.Syntax` | Heading — `.Level` (1–6), `.Inline` for text |
| `ParagraphBlock` | `Markdig.Syntax` | Paragraph of text |
| `FencedCodeBlock` | `Markdig.Syntax` | Fenced code block |
| `YamlFrontMatterBlock` | `Markdig.Extensions.Yaml` | YAML `---...---` block |

### AST Structure

The `MarkdownDocument` is a **flat list of top-level blocks** — headings, paragraphs, code blocks are all siblings. Headings do **not** create nested sections (unlike HTML's implied outline). Extracting a "section" requires walking the flat list and collecting blocks between heading boundaries.

Every `MarkdownObject` has `.Line`, `.Column`, and `.Span` (start/end offsets into the source string).

### Pipeline Configuration

```csharp
using Markdig;

var pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()    // Markdig.Extensions.Yaml
    .UseAdvancedExtensions() // tables, footnotes, task lists, etc.
    .Build();

// Pipeline is immutable and thread-safe — cache and reuse it
MarkdownDocument doc = Markdown.Parse(markdownText, pipeline);
```

### Section Extraction by Header

A section starts at a `HeadingBlock` matching the target text and ends just before the next heading of the same or higher (lower number) level, or at end of document.

```csharp
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

public static class MarkdownSectionExtractor
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAdvancedExtensions()
        .Build();

    public static MarkdownDocument Parse(string markdown)
        => Markdown.Parse(markdown, Pipeline);

    /// <summary>
    /// Extracts blocks belonging to a section identified by header text.
    /// Section starts at the matched HeadingBlock and ends before the next
    /// heading of same or higher level, or at end of document.
    /// </summary>
    public static List<Block> GetSectionBlocks(
        MarkdownDocument doc,
        string headerText,
        StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        var blocks = new List<Block>();
        int? sectionLevel = null;
        bool capturing = false;

        foreach (var block in doc)
        {
            if (block is HeadingBlock heading)
            {
                string text = GetHeadingPlainText(heading);

                if (!capturing)
                {
                    if (text.Equals(headerText, comparison))
                    {
                        capturing = true;
                        sectionLevel = heading.Level;
                        blocks.Add(block);
                    }
                }
                else
                {
                    // Stop at same or higher level heading
                    if (heading.Level <= sectionLevel)
                        break;
                    blocks.Add(block);
                }
            }
            else if (capturing)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    /// <summary>
    /// Gets the raw source text for a section using Span offsets.
    /// </summary>
    public static string GetSectionRawText(
        string originalMarkdown,
        MarkdownDocument doc,
        string headerText)
    {
        var sectionBlocks = GetSectionBlocks(doc, headerText);
        if (sectionBlocks.Count == 0)
            return string.Empty;

        int start = sectionBlocks[0].Span.Start;
        int end = sectionBlocks[^1].Span.End;

        // Span.End is inclusive
        return originalMarkdown[start..(end + 1)];
    }

    /// <summary>
    /// Extracts YAML front matter as a raw string.
    /// Deserialize with YamlDotNet separately.
    /// </summary>
    public static string? GetYamlFrontMatter(MarkdownDocument doc, string originalMarkdown)
    {
        var yamlBlock = doc.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
        if (yamlBlock is null)
            return null;

        var lines = yamlBlock.Lines;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Count; i++)
            sb.AppendLine(lines.Lines[i].Slice.ToString());

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extracts plain text from a heading, handling inline formatting.
    /// </summary>
    private static string GetHeadingPlainText(HeadingBlock heading)
    {
        if (heading.Inline is null)
            return string.Empty;

        // For simple headings, FirstChild is a LiteralInline
        // For formatted headings (e.g., # Hello **world**), walk all literals
        var sb = new System.Text.StringBuilder();
        foreach (var inline in heading.Inline.Descendants<LiteralInline>())
            sb.Append(inline.Content);

        return sb.ToString();
    }
}
```

### Usage Example

```csharp
var markdown = """
    ---
    name: auth
    description: Authentication module
    ---

    # Authentication Specification

    ## Overview

    High-level purpose and scope.

    ## Acceptance Criteria

    - [ ] OAuth flow works end-to-end
    - [ ] Token refresh handles expiry

    ## Dependencies

    Other modules this depends on.
    """;

var doc = MarkdownSectionExtractor.Parse(markdown);

// Extract YAML frontmatter
string? yaml = MarkdownSectionExtractor.GetYamlFrontMatter(doc, markdown);
// → "name: auth\ndescription: Authentication module"

// Extract section by header
string section = MarkdownSectionExtractor.GetSectionRawText(markdown, doc, "Acceptance Criteria");
// → "## Acceptance Criteria\n\n- [ ] OAuth flow works end-to-end\n..."

// List all headings
foreach (var heading in doc.Descendants<HeadingBlock>())
{
    var text = heading.Inline?.FirstChild?.ToString();
    Console.WriteLine($"H{heading.Level}: {text}");
}
```

### Gotchas

- **Flat AST**: Headings don't nest. Must walk linearly and track levels.
- **`HeadingBlock.Inline`**: Linked list of `Inline` objects. For formatted headings, use `Descendants<LiteralInline>()` to extract all text.
- **`.Span` for raw text**: Most reliable way to extract exact original text including formatting.
- **`.Descendants<T>()`**: Extension method on `MarkdownObject` — use for recursive search.
- **YAML parsing**: Markdig only parses boundaries. Use **YamlDotNet** to deserialize the content.
- **Thread safety**: `MarkdownPipeline` is immutable after `Build()` — cache and reuse.

---

## 2. Regex-Based Approach (Alternative)

A lightweight alternative when Markdig is too heavy or not needed.

### Pattern for Splitting on Headers

```csharp
using System.Text.RegularExpressions;

public static partial class RegexMarkdownParser
{
    /// <summary>
    /// Extracts a section by header text. Section ends at next header of
    /// same or higher level, or end of document.
    /// </summary>
    public static string? ExtractSection(string markdown, string headerText, int headerLevel = 0)
    {
        // Strip YAML frontmatter first
        markdown = StripYamlFrontMatter(markdown);

        // Build pattern: match the target header, capture everything until
        // next header of same or higher level
        string levelPattern = headerLevel > 0
            ? $"{{1,{headerLevel}}}"
            : "+";

        var pattern = $@"^(#{levelPattern})\s+{Regex.Escape(headerText)}\s*$" +
                      $@"([\s\S]*?)(?=^#{{1,{(headerLevel > 0 ? headerLevel : 6)}}}\s|\z)";

        var match = Regex.Match(markdown, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        return match.Success ? match.Value.Trim() : null;
    }

    /// <summary>
    /// Strips YAML frontmatter (--- ... ---) from the beginning of a markdown string.
    /// </summary>
    public static string StripYamlFrontMatter(string markdown)
    {
        var match = YamlFrontMatterPattern().Match(markdown);
        return match.Success ? markdown[match.Length..].TrimStart() : markdown;
    }

    /// <summary>
    /// Extracts YAML frontmatter content (without delimiters).
    /// </summary>
    public static string? ExtractYamlFrontMatter(string markdown)
    {
        var match = YamlFrontMatterPattern().Match(markdown);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"\A---\s*\n([\s\S]*?)\n---\s*\n", RegexOptions.Multiline)]
    private static partial Regex YamlFrontMatterPattern();
}
```

### Pros & Cons vs Markdig

| | Markdig | Regex |
|---|---|---|
| **Dependencies** | NuGet package | None (BCL only) |
| **Correctness** | Full AST — handles edge cases (nested code blocks, indented headings) | Fragile — headings inside fenced code blocks produce false matches |
| **Performance** | Fast (optimized parser) | Faster for simple cases, but backtracking risk on large docs |
| **YAML frontmatter** | Extension support | Manual regex |
| **Maintenance** | Well-maintained library | Custom code to maintain |
| **Section nesting** | Precise level tracking via `HeadingBlock.Level` | Regex level matching is error-prone |

**Recommendation**: Use **Markdig** for production. Regex is acceptable for quick prototyping or when minimizing dependencies is critical, but it will misidentify headings inside fenced code blocks (e.g., ```` ```markdown\n# Not a real heading\n``` ````).

---

## 3. Content Hashing for Drift Detection

### Algorithm Comparison

| | SHA256 | XxHash128 | XxHash64 |
|---|---|---|---|
| **BCL in .NET 8** | ✅ `System.Security.Cryptography` | ✅ `System.IO.Hashing` | ✅ `System.IO.Hashing` |
| **Speed** | ~500 MB/s | ~15 GB/s | ~30 GB/s |
| **Output** | 64 hex chars (256 bits) | 32 hex chars (128 bits) | 16 hex chars (64 bits) |
| **Crypto-safe** | ✅ | ❌ | ❌ |
| **NuGet needed (.NET 8)** | No | No | No |

**Recommendation**: Use **XxHash128**. It's 20–60× faster than SHA256, in the .NET 8 BCL, 128 bits provides negligible collision probability for content addressing, and cryptographic security is unnecessary for drift detection.

### Content Normalization

Normalization prevents false-positive drift alerts from whitespace/line-ending differences across platforms and editors.

```csharp
using System.Text.RegularExpressions;

public static partial class ContentNormalizer
{
    public static string Normalize(string content)
    {
        // 1. Normalize line endings (Windows \r\n → Unix \n)
        content = content.ReplaceLineEndings("\n");

        // 2. Trim leading/trailing whitespace
        content = content.Trim();

        // 3. Collapse 3+ consecutive newlines into 2 (one blank line)
        content = MultipleBlankLines().Replace(content, "\n\n");

        return content;
    }

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleBlankLines();
}
```

### Hashing Implementation

```csharp
using System.IO.Hashing;
using System.Text;

public static class ContentHasher
{
    /// <summary>
    /// Computes a normalized content hash for drift detection.
    /// Uses XxHash128 for speed — no cryptographic requirement.
    /// </summary>
    public static string ComputeHash(string content)
    {
        string normalized = ContentNormalizer.Normalize(content);
        byte[] hash = XxHash128.Hash(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Checks if content has drifted from a previously stored hash.
    /// </summary>
    public static bool HasDrifted(string currentContent, string previousHash)
        => !ComputeHash(currentContent).Equals(previousHash, StringComparison.OrdinalIgnoreCase);
}
```

---

## 4. Section Cache Strategy

Per the storage specification, cache is keyed by `file path + section header + file modification timestamp` under `.lopen/cache/sections/`.

```csharp
using System.IO.Hashing;
using System.Text;

public sealed record SectionCacheKey(string FilePath, string Header, DateTimeOffset LastModified)
{
    /// <summary>
    /// Generates a deterministic cache file name from the composite key.
    /// </summary>
    public string ToCacheFileName()
    {
        string input = $"{FilePath}|{Header}|{LastModified:O}";
        byte[] hash = XxHash64.Hash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);  // 16-char hex, e.g., "A1B2C3D4E5F6A7B8"
    }
}

public sealed record CachedSection(
    string FilePath,
    string Header,
    DateTimeOffset LastModified,
    string Content,
    string ContentHash);
```

Cache invalidation is automatic: when the source file's modification timestamp changes, the cache key no longer matches, so a fresh extraction is performed.

---

## 5. Integrated Example

Complete flow: parse → extract → normalize → hash → cache.

```csharp
public sealed class SpecificationParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Extracts a section from a SPECIFICATION.md file, computes its content hash,
    /// and returns a cacheable result.
    /// </summary>
    public CachedSection? ExtractSection(string filePath, string headerText)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
            return null;

        string markdown = File.ReadAllText(filePath);
        var doc = Markdown.Parse(markdown, Pipeline);

        string sectionText = MarkdownSectionExtractor.GetSectionRawText(markdown, doc, headerText);
        if (string.IsNullOrEmpty(sectionText))
            return null;

        string contentHash = ContentHasher.ComputeHash(sectionText);

        return new CachedSection(
            FilePath: filePath,
            Header: headerText,
            LastModified: fileInfo.LastWriteTimeUtc,
            Content: sectionText,
            ContentHash: contentHash);
    }

    /// <summary>
    /// Detects specification drift by comparing current section hash against stored hash.
    /// </summary>
    public bool DetectDrift(string filePath, string headerText, string previousHash)
    {
        var section = ExtractSection(filePath, headerText);
        if (section is null)
            return true; // Section was removed — that's drift

        return section.ContentHash != previousHash;
    }
}
```

---

## 6. Decision Summary

| Decision | Choice | Rationale |
|---|---|---|
| **Markdown parser** | Markdig | Full AST, YAML extension, battle-tested, handles edge cases |
| **Hash algorithm** | XxHash128 | BCL in .NET 8, 20–60× faster than SHA256, sufficient for content addressing |
| **Normalization** | Line endings + trim + collapse blanks | Prevents false-positive drift from formatting differences |
| **Cache key** | File path + header + modification timestamp | Matches storage specification; auto-invalidates on file change |
| **Regex fallback** | Not recommended for production | Fails on headings inside fenced code blocks |
