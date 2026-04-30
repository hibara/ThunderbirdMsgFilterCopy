using System.Collections.Generic;
using System.IO;

namespace ThunderbirdMsgFilterCopy.Core;

/// <summary>
/// Minimal INI reader sufficient for Thunderbird's profiles.ini / installs.ini.
/// - Section lines: [Name]
/// - Key-value lines: key=value  (first '=' splits)
/// - Lines starting with ';' or '#' are comments.
/// Not a general INI parser — we only need case-sensitive keys under named sections.
/// </summary>
public static class IniFile
{
    /// <summary>
    /// Parses an INI file into { sectionName → { key → value } }.
    /// Returns an empty dictionary on any I/O or format error — callers treat missing data
    /// as "no profile detected" rather than surfacing an exception.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Read(string path)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>();
        if (!File.Exists(path))
        {
            return Wrap(sections);
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch
        {
            return Wrap(sections);
        }

        string current = string.Empty;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
            {
                continue;
            }
            if (line[0] == '[' && line[^1] == ']')
            {
                current = line[1..^1].Trim();
                if (!sections.ContainsKey(current))
                {
                    sections[current] = new Dictionary<string, string>();
                }
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0 || current.Length == 0)
            {
                continue;
            }
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            sections[current][key] = value;
        }

        return Wrap(sections);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Wrap(
        Dictionary<string, Dictionary<string, string>> sections)
    {
        var ro = new Dictionary<string, IReadOnlyDictionary<string, string>>(sections.Count);
        foreach (var kv in sections)
        {
            ro[kv.Key] = kv.Value;
        }
        return ro;
    }
}
