namespace Lopen;

/// <summary>
/// Discovers the project root directory by walking up from a starting directory
/// to find the nearest parent containing <c>.lopen/</c> or <c>.git/</c>.
/// </summary>
public static class ProjectRootDiscovery
{
    /// <summary>
    /// Finds the project root by walking up from <paramref name="startDirectory"/>
    /// looking for <c>.lopen/</c> (preferred) then <c>.git/</c>.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from.</param>
    /// <returns>
    /// The path to the directory containing the marker, or <c>null</c> if neither
    /// <c>.lopen/</c> nor <c>.git/</c> is found in any ancestor.
    /// </returns>
    public static string? FindProjectRoot(string startDirectory)
    {
        // First pass: look for .lopen/ (strongest signal — user explicitly initialized)
        var lopenRoot = FindMarker(startDirectory, ".lopen");
        if (lopenRoot is not null)
            return lopenRoot;

        // Second pass: look for .git/ (fallback for any git repo)
        return FindMarker(startDirectory, ".git");
    }

    private static string? FindMarker(string startDirectory, string markerName)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var markerPath = Path.Combine(current.FullName, markerName);
            if (Directory.Exists(markerPath))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
