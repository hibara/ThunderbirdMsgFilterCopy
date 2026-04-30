using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ThunderbirdMsgFilterCopy.Resources;
using ThunderbirdMsgFilterCopy.Services;

namespace ThunderbirdMsgFilterCopy.Views;

/// <summary>
/// About dialog (SPEC §8.2.5). All displayed text comes from <see cref="AppInfo"/>
/// (assembly attributes) or localized <see cref="Strings"/> — nothing is hardcoded in XAML.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        Title = string.Format(CultureInfo.CurrentCulture, Strings.AboutWindowTitleFormat, AppInfo.Product);

        using (var stream = AssetLoader.Open(new Uri(AppInfo.AboutIconUri)))
        {
            AboutIconImage.Source = new Bitmap(stream);
        }
        ProductText.Text = AppInfo.Product;
        VersionText.Text = string.Format(CultureInfo.CurrentCulture, Strings.AboutVersionFormat, AppInfo.DisplayVersion);
        CopyrightText.Text = AppInfo.Copyright;
        OkButton.Content = Strings.CommonOk;

        OkButton.Click += (_, _) => Close();
    }
}
