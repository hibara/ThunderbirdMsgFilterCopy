using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ThunderbirdMsgFilterCopy.Core.Models;
using ThunderbirdMsgFilterCopy.Resources;

namespace ThunderbirdMsgFilterCopy.ViewModels;

/// <summary>
/// UI wrapper around <see cref="ThunderbirdProfile"/> that formats the dropdown label
/// with localized badges (SPEC §3.1.5). The underlying model is kept free of resx
/// dependencies so the Core assembly stays UI-agnostic.
/// </summary>
public sealed partial class ProfileViewItem : ObservableObject
{
    [ObservableProperty]
    private string _label = string.Empty;

    private int _totalFilterCount;

    public ProfileViewItem(ThunderbirdProfile profile)
    {
        Profile = profile;
        _totalFilterCount = profile.TotalFilterCount;
        RebuildLabel();
    }

    public ThunderbirdProfile Profile { get; }

    /// <summary>
    /// Re-evaluate the badge using a fresh total filter count (SPEC §3.1.5: same triggers as
    /// §3.5.5 — profile switch / import complete / undo complete). Called after the export
    /// account list has been re-scanned so the badge stays in sync with the per-row counts.
    /// </summary>
    public void UpdateTotalFilterCount(int totalFilterCount)
    {
        if (_totalFilterCount == totalFilterCount) return;
        _totalFilterCount = totalFilterCount;
        RebuildLabel();
    }

    private void RebuildLabel()
    {
        var badges = string.Empty;
        if (Profile.IsInUse) badges += Strings.ProfileBadgeInUse;
        if (Profile.IsEmpty) badges += Strings.ProfileBadgeEmpty;
        // Show "[フィルター: N件]" whenever any msgFilterRules.dat exists, even when N=0
        // (so a profile with only zero-filter accounts still surfaces the count). SPEC §3.1.5.
        if (Profile.HasFilterFiles)
        {
            badges += string.Format(
                CultureInfo.CurrentCulture,
                Strings.ProfileBadgeFilterCountFormat,
                _totalFilterCount);
        }
        Label = Profile.Name + badges;
    }

    public override string ToString() => Label;
}
