using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.6: opens a .tbfilters package, computes the match against the current profile,
/// and — on Execute — performs the copy + backup dance.
/// Matching is exact (serverFolderName, accountType) only (SPEC §3.6.3).
/// </summary>
public interface IFilterImporter
{
    Task<ImportPlan> OpenAsync(string packagePath, ThunderbirdProfile targetProfile, CancellationToken ct = default);

    Task<ImportResult> ExecuteAsync(ImportPlan plan, CancellationToken ct = default);
}

public sealed record ImportPlan(
    ExportManifest Manifest,
    IReadOnlyList<ImportPlanEntry> Entries,
    string PackagePath);

public sealed record ImportPlanEntry(
    ManifestEntry Source,
    ThunderbirdAccount? Target,          // null when unmatched → skipped
    string? TargetVersion,               // null when target has no existing file
    bool VersionMismatch);

public sealed record ImportResult(int Overwritten, int Skipped, IReadOnlyList<string> Errors);
