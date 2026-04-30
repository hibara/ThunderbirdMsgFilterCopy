using System;
using System.Collections.Generic;

namespace ThunderbirdMsgFilterCopy.Core.Models;

/// <summary>
/// SPEC §3.7.3: session.json. Restore logic keys off <see cref="BackupEntry.HadExistingFile"/>
/// — false means "delete the file at OriginalFilePath" (target had nothing originally).
/// </summary>
public sealed record BackupSession
{
    public required DateTimeOffset ExecutedAt { get; init; }
    public required string ProfilePath { get; init; }
    public required IReadOnlyList<BackupEntry> Entries { get; init; }
}

public sealed record BackupEntry
{
    public required int Index { get; init; }
    public required AccountType AccountType { get; init; }
    public required string ServerFolderName { get; init; }
    public required string OriginalFilePath { get; init; }   // absolute
    public required string BackupFilePath { get; init; }     // relative to backup root
    public required bool HadExistingFile { get; init; }
}
