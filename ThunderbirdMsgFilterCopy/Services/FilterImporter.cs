using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ThunderbirdMsgFilterCopy.Core;
using ThunderbirdMsgFilterCopy.Core.Models;
using ThunderbirdMsgFilterCopy.Resources;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class FilterImporter(
    IAccountScanner accountScanner,
    IBackupManager backupManager,
    IThunderbirdMonitor monitor) : IFilterImporter
{
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public async Task<ImportPlan> OpenAsync(string packagePath, ThunderbirdProfile targetProfile, CancellationToken ct = default)
    {
        if (!File.Exists(packagePath))
        {
            throw new ImportPackageException(Strings.ImportPackageErrorFileNotFound);
        }

        ExportManifest manifest;
        try
        {
            await using var fs = File.OpenRead(packagePath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var mEntry = zip.GetEntry("manifest.json") ?? throw new ImportPackageException(
                Strings.ImportPackageErrorCorrupted);
            await using var ms = mEntry.Open();
            manifest = (await JsonSerializer.DeserializeAsync<ExportManifest>(ms, JsonOptions, ct))
                       ?? throw new ImportPackageException(Strings.ImportPackageErrorCorrupted);
        }
        catch (ImportPackageException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImportPackageException(Strings.ImportPackageErrorCorrupted, ex);
        }

        if (manifest.SchemaVersion != SupportedSchemaVersion)
        {
            throw new ImportPackageException(Strings.ImportPackageErrorUnsupportedSchema);
        }

        // SPEC §3.6.3: match exact (serverFolderName, accountType) against current profile.
        var existing = accountScanner.Scan(targetProfile.Path);
        var index = existing.ToDictionary(
            a => (a.ServerFolderName, a.AccountType),
            a => a);

        var entries = new List<ImportPlanEntry>(manifest.Entries.Count);
        foreach (var me in manifest.Entries)
        {
            ThunderbirdAccount? target = index.TryGetValue((me.ServerFolderName, me.AccountType), out var t) ? t : null;
            string? targetVersion = target?.FileVersion;
            bool mismatch = target is not null
                            && !string.IsNullOrEmpty(targetVersion)
                            && !string.IsNullOrEmpty(me.FileVersion)
                            && targetVersion != me.FileVersion;
            entries.Add(new ImportPlanEntry(me, target, targetVersion, mismatch));
        }

        return new ImportPlan(manifest, entries, packagePath);
    }

    public async Task<ImportResult> ExecuteAsync(ImportPlan plan, CancellationToken ct = default)
    {
        // SPEC §3.6.6 step 1: re-check Thunderbird is not running.
        if (monitor.IsRunning)
        {
            throw new ImportPackageException(Strings.ImportPackageErrorThunderbirdRunning);
        }

        // The target profile path is implicit via the entry target; but we store it for the
        // backup session by using the directory above the first matched file.
        var firstMatched = plan.Entries.FirstOrDefault(e => e.Target is not null);
        if (firstMatched?.Target is null)
        {
            return new ImportResult(0, plan.Entries.Count, Array.Empty<string>());
        }
        var profilePath = FindProfilePath(firstMatched.Target.FilterFilePath);
        backupManager.StartNewSession(profilePath);

        await using var fs = File.OpenRead(plan.PackagePath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

        int overwritten = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var entry in plan.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (entry.Target is null)
            {
                skipped++;
                continue;
            }

            var zipPath = $"filters/{entry.Source.Index:D4}/msgFilterRules.dat";
            var ze = zip.GetEntry(zipPath);
            if (ze is null)
            {
                errors.Add(string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ImportPackageErrorMissingZipEntryFormat,
                    entry.Source.ServerFolderName));
                skipped++;
                continue;
            }

            var targetPath = entry.Target.FilterFilePath;
            var hadExisting = File.Exists(targetPath);
            try
            {
                backupManager.AddEntry(entry.Target, targetPath, hadExisting);

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await using var src = ze.Open();
                await using var dst = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await src.CopyToAsync(dst, ct);
                overwritten++;
            }
            catch (Exception ex)
            {
                errors.Add($"{entry.Source.ServerFolderName}: {ex.Message}");
            }
        }

        backupManager.CommitSession();
        return new ImportResult(overwritten, skipped, errors);
    }

    /// <summary>
    /// Walk up from a filter file path (…/Profiles/xxx/Mail/foo/msgFilterRules.dat) to the
    /// profile root (…/Profiles/xxx). SPEC-level we only need the root for session.json.
    /// </summary>
    private static string FindProfilePath(string filterFilePath)
    {
        // filterFilePath = <profile>/(Mail|ImapMail)/<accountDir>/msgFilterRules.dat
        var accountDir = Path.GetDirectoryName(filterFilePath) ?? string.Empty;
        var mailLikeDir = Path.GetDirectoryName(accountDir) ?? string.Empty;
        return Path.GetDirectoryName(mailLikeDir) ?? mailLikeDir;
    }
}

/// <summary>
/// Thrown for user-facing package errors. The message is safe to show in a dialog directly
/// (SPEC §6.2 / §6.3).
/// </summary>
public sealed class ImportPackageException : Exception
{
    public ImportPackageException(string message) : base(message) { }
    public ImportPackageException(string message, Exception inner) : base(message, inner) { }
}
