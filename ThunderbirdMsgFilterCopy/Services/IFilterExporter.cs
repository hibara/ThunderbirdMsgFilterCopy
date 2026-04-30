using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// SPEC §3.5 / §4.1: packs selected accounts' msgFilterRules.dat into a .tbfilters ZIP.
/// </summary>
public interface IFilterExporter
{
    Task ExportAsync(
        IReadOnlyList<ThunderbirdAccount> accounts,
        string destinationPath,
        ThunderbirdProfile sourceProfile,
        CancellationToken ct = default);
}
