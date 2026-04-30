using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class FilterExporter : IFilterExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public async Task ExportAsync(
        IReadOnlyList<ThunderbirdAccount> accounts,
        string destinationPath,
        ThunderbirdProfile sourceProfile,
        CancellationToken ct = default)
    {
        // Build manifest entries while streaming files into the ZIP. SPEC §4.1.
        var entries = new List<ManifestEntry>(accounts.Count);

        // Write to a temp file first so a mid-write failure doesn't leave a corrupt target.
        var tempPath = destinationPath + ".tmp";
        if (File.Exists(tempPath)) File.Delete(tempPath);

        await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: false))
        {
            for (int i = 0; i < accounts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var a = accounts[i];
                var folderName = i.ToString("D4");
                var entry = zip.CreateEntry($"filters/{folderName}/msgFilterRules.dat", CompressionLevel.Optimal);

                await using (var src = File.OpenRead(a.FilterFilePath))
                await using (var dst = entry.Open())
                {
                    await src.CopyToAsync(dst, ct);
                }

                entries.Add(new ManifestEntry
                {
                    Index = i,
                    AccountType = a.AccountType,
                    ServerFolderName = a.ServerFolderName,
                    UserName = a.UserName,
                    FileVersion = a.FileVersion ?? string.Empty,
                    FilterCount = a.FilterCount,
                });
            }

            var manifest = new ExportManifest
            {
                SchemaVersion = 1,
                ExportedAt = DateTimeOffset.Now,
                SourceOs = OperatingSystem.IsWindows() ? "Windows" : "macOS",
                SourceProfile = sourceProfile.Name,
                Entries = entries,
            };

            var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
            await using (var mStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(mStream, manifest, JsonOptions, ct);
            }
        }

        if (File.Exists(destinationPath)) File.Delete(destinationPath);
        File.Move(tempPath, destinationPath);
    }
}
