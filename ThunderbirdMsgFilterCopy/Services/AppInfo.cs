using System.Reflection;

namespace ThunderbirdMsgFilterCopy.Services;

/// <summary>
/// Thin accessor around the assembly's informational attributes (SPEC §8.2.4).
/// All assembly metadata is declared in the .csproj; the SDK auto-generates the
/// corresponding attributes, which this class reads back via reflection.
/// </summary>
public static class AppInfo
{
    private static readonly Assembly ThisAssembly = typeof(AppInfo).Assembly;

    public static string Product =>
        ThisAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";

    public static string Copyright =>
        ThisAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    public static string Description =>
        ThisAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

    /// <summary>
    /// 表示用バージョン。InformationalVersion から "+" 以降 (Git ハッシュ等) を除去したもの。
    /// </summary>
    public static string DisplayVersion
    {
        get
        {
            var info = ThisAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "";
            var plusIndex = info.IndexOf('+');
            return plusIndex >= 0 ? info[..plusIndex] : info;
        }
    }

    /// <summary>
    /// About ダイアログで表示するアイコンの avares URI。OS により切り替える。
    /// Windows と macOS で見栄えの異なるアイコンを使い分けるため、XAML へ
    /// URI をハードコードせずこのプロパティ経由で取得すること。
    /// </summary>
    public static string AboutIconUri =>
        OperatingSystem.IsMacOS()
            ? "avares://ThunderbirdMsgFilterCopy/Assets/main-icon-mac_64x64.png"
            : "avares://ThunderbirdMsgFilterCopy/Assets/main-icon_64x64.png";
}
