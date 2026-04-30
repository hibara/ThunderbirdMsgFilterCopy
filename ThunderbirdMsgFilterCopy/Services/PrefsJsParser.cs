using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed partial class PrefsJsParser : IPrefsJsParser
{
    // SPEC §3.3.2: user_pref("key", value);
    [GeneratedRegex("""user_pref\("([^"]+)",\s*(.+)\);""")]
    private static partial Regex UserPrefRegex();

    // Keys we care about: mail.server.serverN.<field>  (SPEC §3.3.3)
    [GeneratedRegex("""^mail\.server\.(server\d+)\.(hostname|userName|type|directory-rel)$""")]
    private static partial Regex ServerKeyRegex();

    public IReadOnlyDictionary<string, string> BuildServerFolderToUserName(string profilePath)
    {
        var prefsJs = Path.Combine(profilePath, "prefs.js");
        if (!File.Exists(prefsJs))
        {
            return new Dictionary<string, string>();
        }

        // Group key fields by serverN.
        var byServer = new Dictionary<string, Dictionary<string, string>>();
        try
        {
            foreach (var line in File.ReadLines(prefsJs))
            {
                var m = UserPrefRegex().Match(line);
                if (!m.Success) continue;

                var keyMatch = ServerKeyRegex().Match(m.Groups[1].Value);
                if (!keyMatch.Success) continue;

                var serverId = keyMatch.Groups[1].Value;
                var field = keyMatch.Groups[2].Value;

                // Only string values are relevant (SPEC §3.3.2).
                var raw = m.Groups[2].Value.Trim();
                if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') continue;
                var value = raw[1..^1];

                if (!byServer.TryGetValue(serverId, out var dict))
                {
                    dict = new Dictionary<string, string>();
                    byServer[serverId] = dict;
                }
                dict[field] = value;
            }
        }
        catch
        {
            // SPEC §3.3.5: non-fatal — fall through and return whatever we have so far
            // (or an empty dict if nothing parsed).
        }

        // directory-rel tail → userName (SPEC §3.3.4).
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in byServer.Values)
        {
            if (!kv.TryGetValue("directory-rel", out var dirRel)) continue;
            if (!kv.TryGetValue("userName", out var userName)) continue;
            var tail = DirectoryTail(dirRel);
            if (tail.Length == 0) continue;
            result[tail] = userName;
        }
        return result;
    }

    private static string DirectoryTail(string directoryRel)
    {
        // directory-rel looks like "[ProfD]ImapMail/imap.gmail.com" or "[ProfD]Mail/Local Folders".
        // We want just the final path segment after the last '/' or '\'.
        var idx = directoryRel.LastIndexOfAny(['/', '\\']);
        return idx < 0 ? string.Empty : directoryRel[(idx + 1)..];
    }
}
