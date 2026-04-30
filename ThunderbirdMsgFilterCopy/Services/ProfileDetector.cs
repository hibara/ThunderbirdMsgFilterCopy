using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderbirdMsgFilterCopy.Core;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class ProfileDetector : IProfileDetector
{
    private static readonly string[] NamingSuffixes = { "-release", "-esr", "-beta", "-daily" };

    /// <summary>SPEC §3.1.1: profile root locations.</summary>
    public static string GetProfileRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Thunderbird");
        }
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Thunderbird");
        }
        throw new PlatformNotSupportedException("SPEC §1: Linux is explicitly out of scope.");
    }

    public IReadOnlyList<ThunderbirdProfile> Detect()
    {
        var root = GetProfileRoot();
        var profilesIniPath = Path.Combine(root, "profiles.ini");
        if (!File.Exists(profilesIniPath))
        {
            return Array.Empty<ThunderbirdProfile>();
        }

        var profilesIni = IniFile.Read(profilesIniPath);
        var installsIni = IniFile.Read(Path.Combine(root, "installs.ini"));

        // Collect Default= paths from installs.ini — each such path gets +100 (SPEC §3.1.2 / §3.1.4).
        // Thunderbird writes paths with forward slashes; normalize to the platform separator.
        var inUsePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in installsIni)
        {
            if (kv.Value.TryGetValue("Default", out var defaultRel))
            {
                inUsePaths.Add(NormalizePath(defaultRel));
            }
        }

        var results = new List<ThunderbirdProfile>();
        foreach (var (section, entries) in profilesIni)
        {
            // Only [ProfileN] sections hold actual profiles.
            if (!section.StartsWith("Profile", StringComparison.Ordinal) || section == "Profile")
            {
                continue;
            }
            if (!entries.TryGetValue("Name", out var name) || !entries.TryGetValue("Path", out var relPath))
            {
                continue;
            }
            var isRelative = !entries.TryGetValue("IsRelative", out var isRel) || isRel == "1";
            var absPath = isRelative
                ? Path.GetFullPath(Path.Combine(root, NormalizePath(relPath)))
                : NormalizePath(relPath);

            if (!Directory.Exists(absPath))
            {
                continue;
            }

            int score = 0;
            bool isInUse = inUsePaths.Contains(NormalizePath(relPath));
            if (isInUse) score += 100;
            if (entries.TryGetValue("Default", out var def) && def == "1") score += 50;

            bool hasMailDir = SafeEnumerate(Path.Combine(absPath, "Mail")).Any()
                              || SafeEnumerate(Path.Combine(absPath, "ImapMail")).Any();
            if (hasMailDir) score += 30;

            var filterFiles = CollectFilterFiles(absPath).ToList();
            if (filterFiles.Count > 0) score += 20;

            if (NamingSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase))) score += 10;

            var latestMtime = filterFiles.Select(f =>
            {
                try { return File.GetLastWriteTimeUtc(f); } catch { return DateTime.MinValue; }
            }).DefaultIfEmpty(DateTime.MinValue).Max();

            bool isEmpty = !hasMailDir;

            // SPEC §3.1.5: badge counts individual filters across all files, not file count.
            // Reuses FilterFileAnalyzer.Read so this stays in sync with per-account counts (§3.5.3).
            int totalFilterCount = filterFiles.Sum(f => FilterFileAnalyzer.Read(f).FilterCount);

            results.Add(new ThunderbirdProfile
            {
                Name = name,
                Path = absPath,
                Score = score,
                IsInUse = isInUse,
                IsEmpty = isEmpty,
                HasFilterFiles = filterFiles.Count > 0,
                TotalFilterCount = totalFilterCount,
                LatestFilterFileMtime = latestMtime,
            });
        }

        // Primary: Score desc. Tiebreak: latest msgFilterRules.dat mtime desc (SPEC §3.1.2).
        return results
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.LatestFilterFileMtime)
            .ToList();
    }

    private static string NormalizePath(string p) =>
        p.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

    private static IEnumerable<string> SafeEnumerate(string path)
    {
        if (!Directory.Exists(path)) yield break;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateDirectories(path); }
        catch { yield break; }
        foreach (var e in entries) yield return e;
    }

    private static IEnumerable<string> CollectFilterFiles(string profilePath)
    {
        foreach (var sub in new[] { "Mail", "ImapMail" })
        {
            var dir = Path.Combine(profilePath, sub);
            foreach (var accountDir in SafeEnumerate(dir))
            {
                var file = Path.Combine(accountDir, "msgFilterRules.dat");
                if (File.Exists(file)) yield return file;
            }
        }
    }
}
