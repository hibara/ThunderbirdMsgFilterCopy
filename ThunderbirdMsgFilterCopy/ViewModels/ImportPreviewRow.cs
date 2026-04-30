using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ThunderbirdMsgFilterCopy.ViewModels;

public partial class ImportPreviewRow : ObservableObject
{
    public required string SourceLabel { get; init; }
    public required string TargetLabel { get; init; }
    public required string UserName { get; init; }
    public required bool IsMatched { get; init; }
    public required string StatusText { get; init; }
    public IBrush StatusBrush => IsMatched ? Brushes.Green : Brushes.Gray;
    public string? VersionWarning { get; init; }
    public bool HasVersionWarning => !string.IsNullOrEmpty(VersionWarning);
}
