using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace GetVer
{
  internal class Program
  {
    private static void Main(string[] args)
    {
      var appFilePath = string.Empty;

      if (args.Length < 1)
      {
        Console.WriteLine("指定の引数が一つもありません。処理を中止します。");
        Environment.Exit(1);
      }
      
      var fString = false;
      foreach(var arg in args)
      {
        if (arg == "-s")
        {
          fString = true;
        }
        else
        {
          if (File.Exists(arg))
          {
            appFilePath = Path.GetFullPath(arg);
          }
        }
      }
      
      if (!File.Exists(appFilePath))
      {
        Console.WriteLine("指定されたファイルが存在しません:" + appFilePath);
        Environment.Exit(1);
      }

      // 実行ファイル
      if (IsExecutableFile(appFilePath))
      {
        var vi = FileVersionInfo.GetVersionInfo(appFilePath);
        //バージョン番号
        //Console.WriteLine("FileVersion:{0}", vi.FileVersion);
        //メジャー、マイナー、ビルド、プライベートパート番号
        Console.WriteLine(fString ? "{0}.{1}.{2}.{3}" :
            "{0}{1}{2}{3}", vi.ProductMajorPart, vi.ProductMinorPart, vi.ProductBuildPart, vi.ProductPrivatePart);
        Environment.Exit(0);

      }
      // .csprojファイル
      else if (IsCsProjFile(appFilePath))
      {
        var version = GetVersion(appFilePath);
        Console.WriteLine(fString ? version : version.Replace(".", ""));
        Environment.Exit(0);
      }
      else
      {
        Console.WriteLine("想定外のファイルが指定されました: " + appFilePath);
        Environment.Exit(1);
      }

    }

    private static bool IsExecutableFile(string path)
    {
      // ファイルが存在し、拡張子が".exe"であるかを確認
      return File.Exists(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCsProjFile(string path)
    {
      // ファイルが存在し、拡張子が".csproj"であるかを確認
      return File.Exists(path) && Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// .csproj の &lt;Version&gt; をそのまま返す。例: "1.0.0"
    /// </summary>
    public static string GetVersion(string csprojPath)
    {
      var doc = XDocument.Load(csprojPath);
      return doc.Descendants("Version").First().Value;
    }

    /// <summary>
    /// .csproj の &lt;Version&gt; からドットを除去した短縮形を返す。
    /// 例: "1.0.0" → "100" (build_and_release.sh の VERSION_SHORT と同じ)
    /// </summary>
    public static string GetVersionShort(string csprojPath) => GetVersion(csprojPath).Replace(".", "");

  }
}
