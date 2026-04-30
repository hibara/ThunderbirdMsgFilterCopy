using System.Threading.Tasks;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// Abstraction for all user-visible dialogs. Implemented per-UI-framework (see
/// <see cref="AvaloniaDialogService"/>). A no-op implementation is used at design time and
/// in unit tests. SPEC §6 mandates all errors surface here, not in log files.
/// </summary>
public interface IDialogService
{
    Task ShowInfoAsync(string title, string message);

    /// <summary>
    /// Error dialog with a main message and collapsible detail (SPEC §6.3). <paramref name="detail"/>
    /// is typically the exception's ToString() — callers can pass it freely.
    /// </summary>
    void ShowError(string message, string? detail = null);

    Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText);

    Task<string?> SaveFileAsync(string title, string defaultFileName, string extension);

    Task<string?> OpenFileAsync(string title, string extension);

    /// <summary>
    /// Displays the About dialog (SPEC §8.2.5). Centralized here so that ViewModels
    /// remain free of <c>Avalonia.Controls</c> dependencies.
    /// </summary>
    Task ShowAboutAsync();
}

/// <summary>Design-time / headless fallback.</summary>
public sealed class NullDialogService : IDialogService
{
    public Task ShowInfoAsync(string title, string message) => Task.CompletedTask;
    public void ShowError(string message, string? detail = null) { }
    public Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText) => Task.FromResult(false);
    public Task<string?> SaveFileAsync(string title, string defaultFileName, string extension) => Task.FromResult<string?>(null);
    public Task<string?> OpenFileAsync(string title, string extension) => Task.FromResult<string?>(null);
    public Task ShowAboutAsync() => Task.CompletedTask;
}
