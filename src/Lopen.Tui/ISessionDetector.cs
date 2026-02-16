namespace Lopen.Tui;

/// <summary>
/// Detects whether a previous active session exists and provides
/// data for the session resume modal.
/// </summary>
public interface ISessionDetector
{
    /// <summary>
    /// Checks for an active (non-complete) session and returns the resume data,
    /// or null if no resumable session exists.
    /// </summary>
    Task<SessionResumeData?> DetectActiveSessionAsync(CancellationToken cancellationToken = default);
}
