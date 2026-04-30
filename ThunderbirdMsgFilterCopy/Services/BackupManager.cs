using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class BackupManager : IBackupManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private string? _currentProfilePath;
    private readonly List<BackupEntry> _pending = new();

    /// <summary>SPEC §3.7.1: backup directory location per OS.</summary>
    public static string GetBackupRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ThunderbirdMsgFilterCopy", "Backup");
        }
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "ThunderbirdMsgFilterCopy", "Backup");
        }
        throw new PlatformNotSupportedException();
    }

    private static string SessionJsonPath => Path.Combine(GetBackupRoot(), "session.json");
    private static string FilesRoot => Path.Combine(GetBackupRoot(), "files");

    public bool HasSession => File.Exists(SessionJsonPath);

    public BackupSession? LoadSession()
    {
        if (!HasSession) return null;
        try
        {
            using var stream = File.OpenRead(SessionJsonPath);
            return JsonSerializer.Deserialize<BackupSession>(stream, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void StartNewSession(string profilePath)
    {
        // SPEC §3.7.2: blow away the previous session wholesale before starting a new one.
        var root = GetBackupRoot();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
        Directory.CreateDirectory(FilesRoot);
        _currentProfilePath = profilePath;
        _pending.Clear();
    }

    public void AddEntry(ThunderbirdAccount account, string targetPath, bool hadExistingFile)
    {
        if (_currentProfilePath is null)
        {
            throw new InvalidOperationException("StartNewSession must be called first.");
        }

        var index = _pending.Count;
        var relBackupDir = Path.Combine("files", index.ToString("D4"));
        var absBackupDir = Path.Combine(GetBackupRoot(), relBackupDir);
        Directory.CreateDirectory(absBackupDir);

        var backupRel = Path.Combine(relBackupDir, "msgFilterRules.dat").Replace('\\', '/');
        if (hadExistingFile)
        {
            File.Copy(targetPath, Path.Combine(GetBackupRoot(), backupRel), overwrite: true);
        }

        _pending.Add(new BackupEntry
        {
            Index = index,
            AccountType = account.AccountType,
            ServerFolderName = account.ServerFolderName,
            OriginalFilePath = targetPath,
            BackupFilePath = backupRel,
            HadExistingFile = hadExistingFile,
        });
    }

    public void CommitSession()
    {
        if (_currentProfilePath is null)
        {
            throw new InvalidOperationException("StartNewSession must be called first.");
        }
        var session = new BackupSession
        {
            ExecutedAt = DateTimeOffset.Now,
            ProfilePath = _currentProfilePath,
            Entries = _pending.ToArray(),
        };
        using var stream = File.Create(SessionJsonPath);
        JsonSerializer.Serialize(stream, session, JsonOptions);
    }

    public void RestoreAndClear()
    {
        var session = LoadSession();
        if (session is null) return;

        foreach (var entry in session.Entries)
        {
            // SPEC §3.7.5 restore logic:
            //   hadExistingFile=true  → overwrite originalFilePath from backup
            //   hadExistingFile=false → delete originalFilePath (there was nothing before)
            var original = entry.OriginalFilePath;
            try
            {
                if (entry.HadExistingFile)
                {
                    var backupAbs = Path.Combine(GetBackupRoot(), entry.BackupFilePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                    File.Copy(backupAbs, original, overwrite: true);
                }
                else
                {
                    if (File.Exists(original)) File.Delete(original);
                }
            }
            catch
            {
                // SPEC §6.1: no file logging. Per-entry restore failures are swallowed here;
                // the caller is expected to surface a single completion dialog.
            }
        }

        // Wipe the backup dir after restore.
        var root = GetBackupRoot();
        if (Directory.Exists(root))
        {
            try { Directory.Delete(root, recursive: true); } catch { /* non-fatal */ }
        }
    }
}
