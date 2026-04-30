using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ThunderbirdMsgFilterCopy.Core.Models;
using ThunderbirdMsgFilterCopy.Resources;
using ThunderbirdMsgFilterCopy.Services;

namespace ThunderbirdMsgFilterCopy.ViewModels;

/// <summary>
/// Preview of a resolved .tbfilters against the current profile (SPEC §3.6.2 / §3.6.3).
/// Matching is exact on (serverFolderName, accountType). userName is display-only.
/// </summary>
public partial class ImportPreviewViewModel : ObservableObject
{
    private readonly IFilterImporter _importer;
    private readonly IBackupManager _backup;
    private readonly IDialogService _dialogs;
    private readonly ImportPlan _plan;
    private readonly ImportViewModel _parent;

    [ObservableProperty] private string _sourceSummary;
    [ObservableProperty] private string _exportedAtDisplay;
    [ObservableProperty] private string _sourceLine;
    [ObservableProperty] private string _exportedAtLine;

    public ObservableCollection<ImportPreviewRow> Rows { get; }

    public bool CanExecute => Rows.Any(r => r.IsMatched);

    public ImportPreviewViewModel(
        IFilterImporter importer,
        IBackupManager backup,
        IDialogService dialogs,
        ImportPlan plan,
        ImportViewModel parent)
    {
        _importer = importer;
        _backup = backup;
        _dialogs = dialogs;
        _plan = plan;
        _parent = parent;

        _sourceSummary = $"{plan.Manifest.SourceOs} / {plan.Manifest.SourceProfile}";
        _exportedAtDisplay = plan.Manifest.ExportedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        _sourceLine = string.Format(CultureInfo.CurrentCulture, Strings.ImportPreviewSourceFormat, _sourceSummary);
        _exportedAtLine = string.Format(CultureInfo.CurrentCulture, Strings.ImportPreviewExportedAtFormat, _exportedAtDisplay);

        Rows = new ObservableCollection<ImportPreviewRow>(plan.Entries.Select(BuildRow));
    }

    [RelayCommand]
    private void Cancel() => _parent.Reset();

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        // SPEC §3.6.5 / §5.7.3: 必ず確認ダイアログ。ラベルは動詞ベース (「実行する」/「キャンセル」)。
        var ok = await _dialogs.ConfirmAsync(
            Strings.ImportConfirmTitle,
            Strings.ImportConfirmMessage,
            Strings.CommonExecute,
            Strings.CommonCancel);
        if (!ok) return;

        try
        {
            var result = await _importer.ExecuteAsync(_plan);
            _parent.NotifyBackupStateChanged();
            // SPEC §3.6.6 step 6: 結果ダイアログ表示前にエクスポート画面の件数を再走査させる。
            _parent.NotifyAccountListInvalidated();

            var details = result.Errors.Count == 0
                ? string.Empty
                : Strings.ImportErrorsHeader + string.Join('\n', result.Errors);

            await _dialogs.ShowInfoAsync(
                Strings.ImportCompleteTitle,
                string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.ImportCompleteMessageFormat,
                    result.Overwritten,
                    result.Skipped)
                + details);

            _parent.Reset();
        }
        catch (ImportPackageException ex)
        {
            await _dialogs.ShowInfoAsync(Strings.ImportExecuteNotPossibleTitle, ex.Message);
        }
        catch (System.Exception ex)
        {
            _dialogs.ShowError(Strings.ImportExecuteErrorGeneric, ex.ToString());
        }
    }

    private static ImportPreviewRow BuildRow(ImportPlanEntry e)
    {
        var sourceTypeStr = e.Source.AccountType.ToString().ToUpperInvariant();
        var sourceLabel = string.Format(
            CultureInfo.CurrentCulture,
            Strings.ImportPreviewSourceLabelFormat,
            e.Source.ServerFolderName,
            sourceTypeStr);
        var targetLabel = e.Target?.ServerFolderName ?? Strings.ImportPreviewTargetEmpty;
        var status = e.Target is null
            ? Strings.ImportPreviewStatusUnmatched
            : string.Format(
                CultureInfo.CurrentCulture,
                Strings.ImportPreviewStatusMatchedFormat,
                e.Source.FilterCount);
        var versionWarning = e.VersionMismatch
            ? string.Format(
                CultureInfo.CurrentCulture,
                Strings.ImportPreviewVersionWarningFormat,
                e.Source.FileVersion,
                e.TargetVersion)
            : null;

        return new ImportPreviewRow
        {
            SourceLabel = sourceLabel,
            TargetLabel = targetLabel,
            UserName = e.Source.UserName ?? string.Empty,
            IsMatched = e.Target is not null,
            StatusText = status,
            VersionWarning = versionWarning,
        };
    }
}
