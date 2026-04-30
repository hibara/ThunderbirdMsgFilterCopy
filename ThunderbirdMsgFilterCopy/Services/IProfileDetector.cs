using System.Collections.Generic;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.1: parses profiles.ini / installs.ini, returns scored profile candidates.
/// The caller (MainWindowViewModel) picks the top-scored one as the default selection
/// but the user can override via the dropdown.
/// </summary>
public interface IProfileDetector
{
    IReadOnlyList<ThunderbirdProfile> Detect();
}
