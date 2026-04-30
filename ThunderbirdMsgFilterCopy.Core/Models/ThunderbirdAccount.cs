namespace ThunderbirdMsgFilterCopy.Core.Models;

/// <summary>
/// A mail account discovered under a profile. <see cref="ServerFolderName"/> is the
/// matching key for import (SPEC §3.6.3) — never hostname, never userName.
/// </summary>
public sealed record ThunderbirdAccount
{
    public required AccountType AccountType { get; init; }
    public required string ServerFolderName { get; init; }   // e.g. "imap.gmail.com"
    public required string FilterFilePath { get; init; }     // absolute path to msgFilterRules.dat
    public string? UserName { get; init; }                   // prefs.js hint — display only
    public int FilterCount { get; init; }                    // name="..." occurrences
    public string? FileVersion { get; init; }                // version="N" from header
}
