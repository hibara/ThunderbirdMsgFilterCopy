using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ThunderbirdMsgFilterCopy.Services;
using ThunderbirdMsgFilterCopy.ViewModels;
using ThunderbirdMsgFilterCopy.Views;

namespace ThunderbirdMsgFilterCopy;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Lightweight manual composition root — DI is single-tenant so a container is overkill.
            var prefs = new PrefsJsParser();
            var scanner = new AccountScanner(prefs);
            var monitor = new ThunderbirdMonitor();
            var backup = new BackupManager();
            var exporter = new FilterExporter();
            var importer = new FilterImporter(scanner, backup, monitor);
            var dialogs = new AvaloniaDialogService();
            var profileDetector = new ProfileDetector();

            var mainVm = new MainWindowViewModel(
                profileDetector, scanner, exporter, importer, backup, monitor, dialogs);

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm,
            };
            desktop.Exit += (_, _) => monitor.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
