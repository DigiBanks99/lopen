namespace Lopen.Tui;

/// <summary>
/// Provides live <see cref="ContextPanelData"/> by aggregating data from
/// plan management, workflow engine, and resource tracking services.
/// </summary>
public interface IContextPanelDataProvider
{
    /// <summary>
    /// Returns the current context panel data snapshot.
    /// Reads from cached state — call <see cref="RefreshAsync"/> to update from live services.
    /// </summary>
    ContextPanelData GetCurrentData();

    /// <summary>
    /// Refreshes cached data from live services (plan tasks, workflow state, etc.).
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the active module whose plan data should be displayed.
    /// Call when the orchestrator starts working on a module.
    /// </summary>
    void SetActiveModule(string moduleName);
}
