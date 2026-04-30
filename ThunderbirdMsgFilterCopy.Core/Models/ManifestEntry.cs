namespace ThunderbirdMsgFilterCopy.Core.Models;

/// <summary>
/// One entry in <see cref="ExportManifest"/>. Corresponds to filters/NNNN/msgFilterRules.dat.
/// <see cref="UserName"/> is display-only and MUST NOT influence matching (SPEC §4.2).
/// </summary>
public sealed record ManifestEntry
{
    public required int Index { get; init; }
    public required AccountType AccountType { get; init; }
    public required string ServerFolderName { get; init; }
    public string? UserName { get; init; }
    public required string FileVersion { get; init; }
    public required int FilterCount { get; init; }
}
