using Avalonia;
using System;
using System.Globalization;

namespace ThunderbirdMsgFilterCopy;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        ApplyLanguageOverride(args);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    // SPEC §8.3.7: --lang <code> / --lang=<code> で UI 言語を強制指定する補助機能。
    // ja のみ ja-JP、それ以外 (en / 不明値) は en-US にフォールバック。
    // --lang 自体が無ければ何もしない (OS 文化に従う既定挙動を維持)。
    private static void ApplyLanguageOverride(string[] args)
    {
        var lang = ParseLangArg(args);
        if (lang is null) return;

        var culture = lang.Trim().Equals("ja", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("ja-JP")
            : new CultureInfo("en-US");

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture   = culture;
    }

    private static string? ParseLangArg(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase))
                return a["--lang=".Length..];
            if (a.Equals("--lang", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                return args[i + 1];
        }
        return null;
    }
}
