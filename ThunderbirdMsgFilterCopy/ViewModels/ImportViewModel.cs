using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderbirdMsgFilterCopy.Core.Models;
using ThunderbirdMsgFilterCopy.Resources;
using ThunderbirdMsgFilterCopy.Services;

namespace ThunderbirdMsgFilterCopy.ViewModels;

/// <summary>
/// Import tab. Initially shows a drop zone (SPEC §3.6.1); switches to the preview
/// ViewModel once a .tbfilters file is accepted (SPEC §3.6.2).
/// </summary>
public partial class ImportViewModel : ObservableObject
{
    private readonly IFilterImporter _importer;
    private readonly IBackupManager _backup;
    private readonly IDialogService _dialogs;
    private readonly Func<ThunderbirdProfile?> _getProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreview))]
    private ImportPreviewViewModel? _preview;

    public ImportViewModel(
        IFilterImporter importer,
        IBackupManager backup,
        IDialogService dialogs,
        Func<ThunderbirdProfile?> getProfile)
    {
        _importer = importer;
        _backup = backup;
        _dialogs = dialogs;
        _getProfile = getProfile;
    }

    public bool HasPreview => Preview is not null;

    public event Action? BackupStateChanged;

    /// <summary>
    /// SPEC §3.5.5 / §3.6.6 step 6: インポート完了時に発火。MainWindowViewModel が
    /// 購読し、エクスポート画面のアカウント一覧を再走査して件数表示を最新化する。
    /// </summary>
    public event Action? AccountListInvalidated;

    [RelayCommand]
    private async Task PickFileAsync()
    {
        var path = await _dialogs.OpenFileAsync(Strings.ImportOpenDialogTitle, "tbfilters");
        if (string.IsNullOrEmpty(path)) return;
        await AcceptAsync(path);
    }

    public async Task AcceptAsync(string path)
    {
        var profile = _getProfile();
        if (profile is null)
        {
            await _dialogs.ShowInfoAsync(Strings.ImportErrorNoProfile, string.Empty);
            return;
        }

        try
        {
            var plan = await _importer.OpenAsync(path, profile);
            Preview = new ImportPreviewViewModel(_importer, _backup, _dialogs, plan, this);
        }
        catch (ImportPackageException ex)
        {
            await _dialogs.ShowInfoAsync(Strings.ImportErrorReadFailedTitle, ex.Message);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Strings.ImportErrorReadFailedGeneric, ex.ToString());
        }
    }

    public void Reset() => Preview = null;

    internal void NotifyBackupStateChanged() => BackupStateChanged?.Invoke();

    internal void NotifyAccountListInvalidated() => AccountListInvalidated?.Invoke();
}
