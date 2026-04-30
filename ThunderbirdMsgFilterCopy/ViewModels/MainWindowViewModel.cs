using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderbirdMsgFilterCopy.Core.Models;
using ThunderbirdMsgFilterCopy.Resources;
using ThunderbirdMsgFilterCopy.Services;

namespace ThunderbirdMsgFilterCopy.ViewModels;

/// <summary>
/// Root ViewModel. Owns shared state (selected profile, Thunderbird running state, backup
/// availability) and hosts the Export / Import tab ViewModels. See SPEC §5.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IProfileDetector _profileDetector;
    private readonly IAccountScanner _accountScanner;
    private readonly IFilterExporter _filterExporter;
    private readonly IFilterImporter _filterImporter;
    private readonly IBackupManager _backupManager;
    private readonly IThunderbirdMonitor _monitor;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    private ObservableCollection<ProfileViewItem> _profiles = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidProfile))]
    private ProfileViewItem? _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndoLastImport))]
    private bool _isThunderbirdRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndoLastImport))]
    private bool _hasBackupSession;

    [ObservableProperty]
    private string _osLabel = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Unknown";

    public ExportViewModel Export { get; }
    public ImportViewModel Import { get; }

    public bool HasValidProfile => SelectedProfile is not null;
    public bool CanUndoLastImport => HasBackupSession && !IsThunderbirdRunning;

    public MainWindowViewModel(
        IProfileDetector profileDetector,
        IAccountScanner accountScanner,
        IFilterExporter filterExporter,
        IFilterImporter filterImporter,
        IBackupManager backupManager,
        IThunderbirdMonitor monitor,
        IDialogService dialogs)
    {
        _profileDetector = profileDetector;
        _accountScanner = accountScanner;
        _filterExporter = filterExporter;
        _filterImporter = filterImporter;
        _backupManager = backupManager;
        _monitor = monitor;
        _dialogs = dialogs;

        Export = new ExportViewModel(filterExporter, dialogs, () => SelectedProfile?.Profile);
        Import = new ImportViewModel(filterImporter, backupManager, dialogs, () => SelectedProfile?.Profile);

        // SPEC §3.5.5: インポート完了時に Export 画面の件数表示を再走査する。
        Import.AccountListInvalidated += RefreshExportAccounts;

        // SPEC §3.7.6: インポート完了時に HasBackupSession を再評価し、
        // 「直前のインポートを取り消す」メニューを有効化する。
        Import.BackupStateChanged += RefreshBackupState;

        _monitor.PropertyChanged += OnMonitorChanged;
        _monitor.Start();
        IsThunderbirdRunning = _monitor.IsRunning;

        LoadProfiles();
        HasBackupSession = _backupManager.HasSession;
    }

    /// <summary>Design-time / fallback ctor (no-arg). Creates an empty VM shell.</summary>
    public MainWindowViewModel() : this(
        new ProfileDetector(),
        new AccountScanner(new PrefsJsParser()),
        new FilterExporter(),
        new FilterImporter(new AccountScanner(new PrefsJsParser()), new BackupManager(), new ThunderbirdMonitor()),
        new BackupManager(),
        new ThunderbirdMonitor(),
        new NullDialogService())
    { }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IThunderbirdMonitor.IsRunning))
        {
            // Marshal to UI thread for property-change notifications.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsThunderbirdRunning = _monitor.IsRunning);
        }
    }

    partial void OnSelectedProfileChanged(ProfileViewItem? value)
    {
        // SPEC §3.5.5: プロファイル切替時もアカウント一覧を再走査する。
        RefreshExportAccounts();
        // Switching profile invalidates any in-progress Import preview.
        Import.Reset();
    }

    /// <summary>
    /// SPEC §3.5.5: 選択中のプロファイル配下を再走査して、Export 画面のアカウント一覧と
    /// フィルター件数を最新化する。プロファイル切替・インポート完了・取り消し完了の
    /// 各タイミングで呼ばれる。件数 0 のアカウントは一覧から除外する (§3.5.2)。
    /// </summary>
    public void RefreshExportAccounts()
    {
        var profile = SelectedProfile;
        if (profile is null)
        {
            Export.LoadAccounts(Array.Empty<ThunderbirdAccount>());
            return;
        }

        try
        {
            var allAccounts = _accountScanner.Scan(profile.Profile.Path);
            // SPEC §3.1.5 / §3.5.5: badge total must equal the sum of per-row (フィルター: N件).
            // Compute the total from the same scan that feeds the export tab so the two values
            // are guaranteed to be derived from a single read of the filesystem.
            profile.UpdateTotalFilterCount(allAccounts.Sum(a => a.FilterCount));
            var accounts = allAccounts
                .Where(a => a.FilterCount > 0)     // SPEC §3.5.2: 件数 0 は一覧に出さない
                .ToList();
            Export.LoadAccounts(accounts);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Strings.AccountScanError, ex.ToString());
            Export.LoadAccounts(Array.Empty<ThunderbirdAccount>());
        }
    }

    private void LoadProfiles()
    {
        try
        {
            var list = _profileDetector.Detect();
            Profiles = new ObservableCollection<ProfileViewItem>(list.Select(p => new ProfileViewItem(p)));
            SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null; // top-scored by Detect() ordering
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Strings.ProfileDetectError, ex.ToString());
            Profiles = new ObservableCollection<ProfileViewItem>();
            SelectedProfile = null;
        }
    }

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndoLastImport))]
    private async Task UndoLastImportAsync()
    {
        // SPEC §3.7.5 step 1: re-check Thunderbird status.
        if (_monitor.IsRunning)
        {
            await _dialogs.ShowInfoAsync(Strings.UndoThunderbirdRunningTitle, Strings.UndoThunderbirdRunningMessage);
            return;
        }

        var session = _backupManager.LoadSession();
        if (session is null)
        {
            HasBackupSession = false;
            return;
        }

        var timestamp = session.ExecutedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        var message = string.Format(
            CultureInfo.CurrentCulture,
            Strings.UndoConfirmMessageFormat,
            timestamp,
            session.Entries.Count);
        var ok = await _dialogs.ConfirmAsync(
            Strings.UndoConfirmTitle,
            message,
            Strings.UndoConfirmOk,
            Strings.CommonCancel);
        if (!ok) return;

        try
        {
            _backupManager.RestoreAndClear();
            // SPEC §3.7.5 step 4: エクスポート画面の件数を再走査する。
            RefreshExportAccounts();
            // SPEC §3.7.5 step 5: HasBackupSession を再評価して取り消しメニューを無効化する。
            RefreshBackupState();
            await _dialogs.ShowInfoAsync(Strings.UndoCompleteTitle, Strings.UndoCompleteMessage);
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Strings.UndoErrorTitle, ex.ToString());
        }
    }

    [RelayCommand]
    private Task ShowAboutAsync() => _dialogs.ShowAboutAsync();

    /// <summary>
    /// SPEC §3.7.6: Backup/session.json の実在を見て HasBackupSession を再評価し、
    /// 「直前のインポートを取り消す」メニューの有効状態を最新化する。
    /// インポート完了時 (Import.BackupStateChanged 経由) と取り消し完了時の双方から呼ばれる。
    /// </summary>
    public void RefreshBackupState() => HasBackupSession = _backupManager.HasSession;
}
