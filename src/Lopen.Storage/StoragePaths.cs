namespace Lopen.Storage;

/// <summary>
/// Resolves storage paths relative to a project root directory.
/// </summary>
public static class StoragePaths
{
    /// <summary>The name of the Lopen storage directory.</summary>
    public const string RootDirectoryName = ".lopen";

    /// <summary>Returns the root .lopen/ directory path.</summary>
    public static string GetRoot(string projectRoot) =>
        Path.Combine(projectRoot, RootDirectoryName);

    /// <summary>Returns the sessions/ directory path.</summary>
    public static string GetSessionsDirectory(string projectRoot) =>
        Path.Combine(GetRoot(projectRoot), "sessions");

    /// <summary>Returns a specific session directory path.</summary>
    public static string GetSessionDirectory(string projectRoot, SessionId sessionId) =>
        Path.Combine(GetSessionsDirectory(projectRoot), sessionId.ToString());

    /// <summary>Returns the path to a session's state.json file.</summary>
    public static string GetSessionStatePath(string projectRoot, SessionId sessionId) =>
        Path.Combine(GetSessionDirectory(projectRoot, sessionId), "state.json");

    /// <summary>Returns the path to a session's metrics.json file.</summary>
    public static string GetSessionMetricsPath(string projectRoot, SessionId sessionId) =>
        Path.Combine(GetSessionDirectory(projectRoot, sessionId), "metrics.json");

    /// <summary>Returns the path to the 'latest' symlink.</summary>
    public static string GetLatestSymlinkPath(string projectRoot) =>
        Path.Combine(GetSessionsDirectory(projectRoot), "latest");

    /// <summary>Returns the modules/ directory path.</summary>
    public static string GetModulesDirectory(string projectRoot) =>
        Path.Combine(GetRoot(projectRoot), "modules");

    /// <summary>Returns a specific module directory path.</summary>
    public static string GetModuleDirectory(string projectRoot, string moduleName) =>
        Path.Combine(GetModulesDirectory(projectRoot), moduleName);

    /// <summary>Returns the path to a module's plan.md file.</summary>
    public static string GetModulePlanPath(string projectRoot, string moduleName) =>
        Path.Combine(GetModuleDirectory(projectRoot, moduleName), "plan.md");

    /// <summary>Returns the cache/ directory path.</summary>
    public static string GetCacheDirectory(string projectRoot) =>
        Path.Combine(GetRoot(projectRoot), "cache");

    /// <summary>Returns the cache/sections/ directory path.</summary>
    public static string GetSectionsCacheDirectory(string projectRoot) =>
        Path.Combine(GetCacheDirectory(projectRoot), "sections");

    /// <summary>Returns the cache/assessments/ directory path.</summary>
    public static string GetAssessmentsCacheDirectory(string projectRoot) =>
        Path.Combine(GetCacheDirectory(projectRoot), "assessments");

    /// <summary>Returns the corrupted/ directory path for quarantined files.</summary>
    public static string GetCorruptedDirectory(string projectRoot) =>
        Path.Combine(GetRoot(projectRoot), "corrupted");

    /// <summary>Returns the project-level config.json path.</summary>
    public static string GetConfigPath(string projectRoot) =>
        Path.Combine(GetRoot(projectRoot), "config.json");
}
