using System.Reflection;
using System.Text.Json;

namespace Lopen.Core;

/// <summary>
/// Service for retrieving and formatting version information.
/// </summary>
public class VersionService
{
    private readonly Assembly _assembly;

    public VersionService() : this(typeof(VersionService).Assembly)
    {
    }

    public VersionService(Assembly assembly)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
    }

    /// <summary>
    /// Gets the semantic version string from the assembly.
    /// </summary>
    public string GetVersion()
    {
        var infoVersion = _assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Strip any +metadata suffix (e.g., "+abc123" from source link)
        if (infoVersion is not null)
        {
            var plusIndex = infoVersion.IndexOf('+');
            if (plusIndex > 0)
            {
                return infoVersion[..plusIndex];
            }
            return infoVersion;
        }

        // Fallback to assembly version
        var version = _assembly.GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "0.0.0";
    }

    /// <summary>
    /// Formats the version for text output.
    /// </summary>
    public string FormatAsText(string appName)
    {
        return $"{appName} version {GetVersion()}";
    }

    /// <summary>
    /// Formats the version for JSON output.
    /// </summary>
    public string FormatAsJson()
    {
        var versionObj = new { version = GetVersion() };
        return JsonSerializer.Serialize(versionObj);
    }
}
