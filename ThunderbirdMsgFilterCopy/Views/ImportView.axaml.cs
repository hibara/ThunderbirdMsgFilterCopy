using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ThunderbirdMsgFilterCopy.ViewModels;

namespace ThunderbirdMsgFilterCopy.Views;

public partial class ImportView : UserControl
{
    public ImportView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // SPEC §3.6.1: drop targets are files only. Folders or non-files are rejected.
        e.DragEffects = e.DataTransfer?.Contains(DataFormat.File) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ImportViewModel vm) return;
        if (e.DataTransfer is null || !e.DataTransfer.Contains(DataFormat.File)) return;

        // §3.6.1: 複数ドロップは最初の 1 件のみ処理。ファイル以外 (フォルダ等) は拒否。
        var first = e.DataTransfer.GetItems(DataFormat.File)
            .Select(item => item.TryGetValue(DataFormat.File))
            .FirstOrDefault(f => f is IStorageFile);

        if (first is null) return;

        var path = first.Path.LocalPath;
        if (System.IO.File.Exists(path))
        {
            await vm.AcceptAsync(path);
        }
    }
}
