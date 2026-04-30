using CommunityToolkit.Mvvm.ComponentModel;
using ThunderbirdMsgFilterCopy.Core.Models;

namespace ThunderbirdMsgFilterCopy.ViewModels;

public partial class ExportAccountItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public required string AccountTypeLabel { get; init; }
    public required string DisplayName { get; init; }
    public required string DetailLine { get; init; }
    public required ThunderbirdAccount Account { get; init; }
}
