using System;
using System.Collections.Generic;

namespace ThunderbirdMsgFilterCopy.Core.Models;

/// <summary>SPEC §4.2. Persisted as <c>manifest.json</c> at the root of a .tbfilters package.</summary>
public sealed record ExportManifest
{
    public int SchemaVersion { get; init; } = 1;
    public required DateTimeOffset ExportedAt { get; init; }
    public required string SourceOs { get; init; }        // "Windows" / "macOS"
    public required string SourceProfile { get; init; }   // profiles.ini Name
    public required IReadOnlyList<ManifestEntry> Entries { get; init; }
}
