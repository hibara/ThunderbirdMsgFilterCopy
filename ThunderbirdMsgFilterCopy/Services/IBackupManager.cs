using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.7: single-session backup. The next import deletes the prior backup wholesale
/// before writing a new one. Restore is the only recovery path — there is no "undo" button
/// inside the result dialog.
/// </summary>
public interface IBackupManager
{
    bool HasSession { get; }

    BackupSession? LoadSession();

    /// <summary>Wipes any existing backup directory and prepares a fresh one.</summary>
    void StartNewSession(string profilePath);

    /// <summary>Copies the target file into the session. <paramref name="hadExistingFile"/> must
    /// be recorded so a later restore knows whether to delete vs. overwrite.</summary>
    void AddEntry(ThunderbirdAccount account, string targetPath, bool hadExistingFile);

    /// <summary>Writes session.json with the accumulated entries.</summary>
    void CommitSession();

    /// <summary>Restores files per session.json then deletes the whole backup dir.</summary>
    void RestoreAndClear();
}
