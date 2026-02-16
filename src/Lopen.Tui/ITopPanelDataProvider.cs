namespace Lopen.Tui;

/// <summary>
/// Provides live <see cref="TopPanelData"/> by aggregating data from
/// token tracking, git, auth, workflow, and model selection services.
/// </summary>
public interface ITopPanelDataProvider
{
    /// <summary>
    /// Returns the current top panel data snapshot.
    /// Reads from cached state — call <see cref="RefreshAsync"/> to update from live services.
    /// </summary>
    TopPanelData GetCurrentData();

    /// <summary>
    /// Refreshes cached data from live services (git branch, auth status, etc.).
    /// Token and workflow data is read synchronously on each call to <see cref="GetCurrentData"/>.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}
