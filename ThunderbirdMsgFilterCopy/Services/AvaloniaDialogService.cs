using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ThunderbirdMsgFilterCopy.Resources;
using ThunderbirdMsgFilterCopy.Views;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// Minimal dialog service using Avalonia built-ins (StorageProvider + a tiny hand-rolled
/// Window-based message box). Deliberately avoids pulling in third-party dialog packages.
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private static Window? Owner =>
        (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public Task ShowInfoAsync(string title, string message) =>
        // SPEC §5.7: 単一ボタンは Esc / Enter どちらでも閉じる (IsCancel と IsDefault を両方 true)。
        ShowMessageAsync(title, message, new[] { (Strings.CommonOk, true, true, true) }, detail: null);

    public void ShowError(string message, string? detail = null)
    {
        // Fire-and-forget — errors can be raised from non-async contexts.
        Dispatcher.UIThread.Post(async () =>
        {
            await ShowMessageAsync(Strings.DialogErrorTitle, message,
                new[] { (Strings.CommonClose, true, true, true) }, detail);
        });
    }

    public async Task<bool> ConfirmAsync(string title, string message, string okText, string cancelText)
    {
        // SPEC §5.7.1 / §5.7.2: ボタン配置は「キャンセル左 / 肯定右」、Esc=Cancel・Enter=OK。
        var result = await ShowMessageAsync(
            title,
            message,
            new[]
            {
                (cancelText, false, /* isCancel: */ true,  /* isDefault: */ false),
                (okText,     true,  /* isCancel: */ false, /* isDefault: */ true),
            },
            detail: null);
        return result;
    }

    public async Task<string?> SaveFileAsync(string title, string defaultFileName, string extension)
    {
        var top = TopLevel.GetTopLevel(Owner);
        if (top is null) return null;

        // macOS の NSSavePanel は SuggestedFileName に拡張子が含まれていて、かつ
        // DefaultExtension も指定されていると、両方を結合して "*.tbfilters.tbfilters"
        // のような二重拡張子で保存してしまうことがある。
        // 防御策として SuggestedFileName 側からは拡張子を剥がし、補完は
        // DefaultExtension にだけ任せる。Windows / Linux でも問題ない。
        var suggested = StripExtension(defaultFileName, extension);

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggested,
            DefaultExtension = extension,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new(extension) { Patterns = new[] { $"*.{extension}" } },
            },
        });

        if (file is null) return null;

        // さらに保険として、戻り値のパスに同じ拡張子が連続して付いていたら 1 個に畳む。
        // (Avalonia / OS 側の将来的な挙動変化に備えるためのフェイルセーフ)
        return NormalizeDoubledExtension(file.Path.LocalPath, extension);
    }

    private static string StripExtension(string fileName, string extension)
    {
        var dotExt = "." + extension;
        while (fileName.EndsWith(dotExt, StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^dotExt.Length];
        }

        return fileName;
    }

    private static string NormalizeDoubledExtension(string path, string extension)
    {
        var dotExt = "." + extension;
        var doubled = dotExt + dotExt;
        while (path.EndsWith(doubled, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^dotExt.Length];
        }

        return path;
    }

    public Task ShowAboutAsync()
    {
        var tcs = new TaskCompletionSource();
        Dispatcher.UIThread.Post(async () =>
        {
            var window = new AboutWindow();
            window.Closed += (_, _) => tcs.TrySetResult();
            if (Owner is { } owner)
            {
                await window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        });
        return tcs.Task;
    }

    public async Task<string?> OpenFileAsync(string title, string extension)
    {
        var top = TopLevel.GetTopLevel(Owner);
        if (top is null) return null;

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new(extension) { Patterns = new[] { $"*.{extension}" } },
                new("All files") { Patterns = new[] { "*" } },
            },
        });
        return files is { Count: > 0 } ? files[0].Path.LocalPath : null;
    }

    /// <summary>
    /// Hand-rolled message window — no external dependency. Buttons are provided as
    /// (label, resultValue, isCancel, isDefault) tuples in the visual order to display.
    /// SPEC §5.7: isCancel=true で Esc に紐づき、isDefault=true で Enter に紐づく。
    /// </summary>
    private static Task<bool> ShowMessageAsync(
        string title,
        string message,
        IReadOnlyList<(string Label, bool Result, bool IsCancel, bool IsDefault)> buttons,
        string? detail)
    {
        var tcs = new TaskCompletionSource<bool>();

        Dispatcher.UIThread.Post(() =>
        {
            var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });

            if (!string.IsNullOrEmpty(detail))
            {
                var expander = new Expander
                {
                    Header = Strings.CommonDetails,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new TextBox
                    {
                        Text = detail,
                        IsReadOnly = true,
                        AcceptsReturn = true,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Height = 140,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                    },
                };
                panel.Children.Add(expander);
            }

            var buttonBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            var window = new Window
            {
                Title = title,
                // SPEC §5.8.1: 確認・通知系ダイアログの共通サイズ規則。
                // Width 固定にしないと SizeToContent で本文長に追従して縮み、
                // Windows のタイトルバー右側コントロール (~130px) に押されて
                // タイトルが「Th...」まで切り詰められてしまう。
                Width = 460,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
            };

            bool settled = false;
            foreach (var (label, result, isCancel, isDefault) in buttons)
            {
                var btn = new Button
                {
                    Content = label,
                    MinWidth = 80,
                    IsCancel = isCancel,
                    IsDefault = isDefault,
                };
                btn.Click += (_, _) =>
                {
                    if (settled) return;
                    settled = true;
                    window.Close();
                    tcs.TrySetResult(result);
                };
                buttonBar.Children.Add(btn);
            }

            var root = new StackPanel();
            root.Children.Add(panel);
            root.Children.Add(new Border { Child = buttonBar, Margin = new Thickness(16, 0, 16, 16) });
            window.Content = root;
            window.Closed += (_, _) =>
            {
                if (!settled) tcs.TrySetResult(false);
            };

            if (Owner is { } owner)
            {
                _ = window.ShowDialog(owner);
            }
            else
            {
                window.Show();
            }
        });

        return tcs.Task;
    }
}
