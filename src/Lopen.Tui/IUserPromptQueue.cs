// Interface moved to Lopen.Core.IUserPromptQueue.
// This file kept for backward compatibility — re-exports the Core interface.
// ReSharper disable once CheckNamespace
namespace Lopen.Tui;

// The Tui module uses the Core-defined interface directly.
// Existing code referencing Lopen.Tui.IUserPromptQueue will find it via the implementation.
