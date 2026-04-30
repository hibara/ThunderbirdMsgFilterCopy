using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderbirdMsgFilterCopy.Core;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

public sealed class AccountScanner(IPrefsJsParser prefsParser) : IAccountScanner
{
    private const string LocalFoldersDirName = "Local Folders";

    public IReadOnlyList<ThunderbirdAccount> Scan(string profilePath)
    {
        var users = prefsParser.BuildServerFolderToUserName(profilePath); // §3.3 — non-fatal on failure

        var accounts = new List<ThunderbirdAccount>();
        Collect(accounts, profilePath, "Mail", isImap: false, users);
        Collect(accounts, profilePath, "ImapMail", isImap: true, users);

        // SPEC §3.2: directories without msgFilterRules.dat are not listed — handled in Collect.
        return accounts;
    }

    private static void Collect(
        List<ThunderbirdAccount> sink,
        string profilePath,
        string subdir,
        bool isImap,
        IReadOnlyDictionary<string, string> users)
    {
        var root = Path.Combine(profilePath, subdir);
        if (!Directory.Exists(root)) return;

        foreach (var accountDir in Directory.EnumerateDirectories(root))
        {
            var filterFile = Path.Combine(accountDir, "msgFilterRules.dat");
            if (!File.Exists(filterFile)) continue;

            var serverFolderName = Path.GetFileName(accountDir);
            var type = isImap
                ? AccountType.Imap
                : (serverFolderName == LocalFoldersDirName ? AccountType.Local : AccountType.Pop);

            var stats = FilterFileAnalyzer.Read(filterFile);

            sink.Add(new ThunderbirdAccount
            {
                AccountType = type,
                ServerFolderName = serverFolderName,
                FilterFilePath = filterFile,
                UserName = users.TryGetValue(serverFolderName, out var u) ? u : null,
                FilterCount = stats.FilterCount,
                FileVersion = stats.Version,
            });
        }
    }
}
