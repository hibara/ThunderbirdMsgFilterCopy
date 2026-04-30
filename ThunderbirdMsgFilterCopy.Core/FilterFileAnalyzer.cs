using System.IO;
using System.Text.RegularExpressions;

namespace ThunderbirdMsgFilterCopy.Core;

/// <summary>
/// Metadata extraction for msgFilterRules.dat. SPEC §4.3: we NEVER parse filter bodies;
/// we only pick out the file header <c>version="N"</c> and count <c>name="..."</c> lines.
/// </summary>
public static partial class FilterFileAnalyzer
{
    [GeneratedRegex("version=\"(\\d+)\"")]
    private static partial Regex VersionRegex();

    [GeneratedRegex("^name=\"", RegexOptions.Multiline)]
    private static partial Regex NameLineRegex();

    public readonly record struct Stats(string? Version, int FilterCount);

    public static Stats Read(string path)
    {
        if (!File.Exists(path))
        {
            return new Stats(null, 0);
        }
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch
        {
            return new Stats(null, 0);
        }
        var vm = VersionRegex().Match(text);
        string? version = vm.Success ? vm.Groups[1].Value : null;
        int count = NameLineRegex().Matches(text).Count;
        return new Stats(version, count);
    }

    public static string? ReadVersion(string path) => Read(path).Version;
}
