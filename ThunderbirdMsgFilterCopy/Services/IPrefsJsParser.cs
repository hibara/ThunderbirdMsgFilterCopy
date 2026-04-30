using System.Collections.Generic;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.3: regex-based extractor for mail.server.serverN.* entries in prefs.js.
/// Failures are non-fatal — callers must treat a missing userName as an empty/null field.
/// </summary>
public interface IPrefsJsParser
{
    /// <summary>
    /// Reads prefs.js and returns a map from the tail segment of <c>directory-rel</c>
    /// (e.g. "imap.gmail.com" from "[ProfD]ImapMail/imap.gmail.com") to <c>userName</c>.
    /// </summary>
    IReadOnlyDictionary<string, string> BuildServerFolderToUserName(string profilePath);
}
