using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
/// Export tab. SPEC §3.5. Initial state: no rows checked. Enabled only when a profile is loaded.
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly IFilterExporter _exporter;
    private readonly IDialogService _dialogs;
    private readonly Func<ThunderbirdProfile?> _getProfile;

    [ObservableProperty]
    private ObservableCollection<ExportAccountItem> _accounts = new();

    public ExportViewModel(IFilterExporter exporter, IDialogService dialogs, Func<ThunderbirdProfile?> getProfile)
    {
        _exporter = exporter;
        _dialogs = dialogs;
        _getProfile = getProfile;
    }

    public bool CanExport => Accounts.Any(a => a.IsSelected);

    public void LoadAccounts(IReadOnlyList<ThunderbirdAccount> accounts)
    {
        foreach (var a in Accounts)
        {
            a.PropertyChanged -= OnItemChanged;
        }
        Accounts = new ObservableCollection<ExportAccountItem>(accounts.Select(ToItem));
        foreach (var a in Accounts)
        {
            a.PropertyChanged += OnItemChanged;
        }
        OnPropertyChanged(nameof(CanExport));
        ExportCommand.NotifyCanExecuteChanged();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExportAccountItem.IsSelected))
        {
            OnPropertyChanged(nameof(CanExport));
            ExportCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var a in Accounts) a.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var a in Accounts) a.IsSelected = false;
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        var profile = _getProfile();
        if (profile is null) return;

        var defaultName = $"thunderbird-filters-{DateTime.Now:yyyyMMdd}.tbfilters";
        var path = await _dialogs.SaveFileAsync(Strings.ExportSaveDialogTitle, defaultName, "tbfilters");
        if (string.IsNullOrEmpty(path)) return;

        var selected = Accounts.Where(a => a.IsSelected).Select(a => a.Account).ToList();
        try
        {
            await _exporter.ExportAsync(selected, path, profile);
            await _dialogs.ShowInfoAsync(
                Strings.ExportCompleteTitle,
                string.Format(CultureInfo.CurrentCulture, Strings.ExportCompleteMessageFormat, selected.Count));
        }
        catch (UnauthorizedAccessException ex)
        {
            _dialogs.ShowError(Strings.ExportErrorNoWritePermission, ex.ToString());
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(Strings.ExportErrorGeneric, ex.ToString());
        }
    }

    private static ExportAccountItem ToItem(ThunderbirdAccount a)
    {
        var typeLabel = a.AccountType switch
        {
            AccountType.Imap => Strings.AccountTypeImap,
            AccountType.Pop => Strings.AccountTypePop,
            AccountType.Local => Strings.AccountTypeLocal,
            _ => "[?]",
        };
        var displayName = a.AccountType == AccountType.Local
            ? Strings.AccountLocalDisplayName
            : a.UserName ?? a.ServerFolderName;
        var detail = string.Format(
            CultureInfo.CurrentCulture,
            Strings.ExportAccountDetailFormat,
            a.ServerFolderName,
            a.FilterCount);
        return new ExportAccountItem
        {
            AccountTypeLabel = typeLabel,
            DisplayName = displayName,
            DetailLine = detail,
            Account = a,
        };
    }
}
