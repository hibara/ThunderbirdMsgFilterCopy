using System.Collections.Generic;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.2: scans Mail/* and ImapMail/* under a profile, emits one entry per directory
/// that actually contains a msgFilterRules.dat. Directories without the file are skipped.
/// </summary>
public interface IAccountScanner
{
    IReadOnlyList<ThunderbirdAccount> Scan(string profilePath);
}
