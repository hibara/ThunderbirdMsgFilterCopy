using System;

namespace ThunderbirdMsgFilterCopy.Core.Models;

/// <summary>
/// A scored profile candidate. See SPEC §3.1.2 for the scoring table.
/// </summary>
public sealed record ThunderbirdProfile
{
    public required string Name { get; init; }
    public required string Path { get; init; }     // absolute on disk
    public required int Score { get; init; }
    public required bool IsInUse { get; init; }    // "[In use]" badge — installs.ini hit
    public required bool IsEmpty { get; init; }    // "[Empty]" badge — no Mail/ImapMail subdirs
    public required bool HasFilterFiles { get; init; } // gates "[Filters: N]" badge visibility
    public required int TotalFilterCount { get; init; } // sum of name="..." across all msgFilterRules.dat (SPEC §3.1.5)
    public DateTime LatestFilterFileMtime { get; init; } // tiebreak key (SPEC §3.1.2)

    /// <summary>
    /// Default label (Name only). Localized badge formatting lives in the UI layer
    /// (see <c>ProfileViewItem</c>) so that Core stays free of resx dependencies.
    /// </summary>
    public override string ToString() => Name;
}
