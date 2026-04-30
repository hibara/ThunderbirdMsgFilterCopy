using System.IO;
using ThunderbirdMsgFilterCopy.Core;

namespace ThunderbirdMsgFilterCopy.Tests;

public class IniFileTests
{
    [Fact]
    public void Parses_profiles_ini_example_from_spec()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            [Profile0]
            Name=default-release
            IsRelative=1
            Path=Profiles/vo4lek9c.default-release

            [Profile1]
            Name=default
            IsRelative=1
            Path=Profiles/ola2w6ng.default

            [General]
            StartWithLastProfile=1
            Version=2
            """);

        var ini = IniFile.Read(path);
        File.Delete(path);

        Assert.Equal("default-release", ini["Profile0"]["Name"]);
        Assert.Equal("Profiles/vo4lek9c.default-release", ini["Profile0"]["Path"]);
        Assert.Equal("default", ini["Profile1"]["Name"]);
        Assert.Equal("1", ini["Profile1"]["IsRelative"]);
        Assert.Equal("2", ini["General"]["Version"]);
    }

    [Fact]
    public void Missing_file_returns_empty()
    {
        var ini = IniFile.Read(Path.Combine(Path.GetTempPath(), "nonexistent-" + System.Guid.NewGuid() + ".ini"));
        Assert.Empty(ini);
    }

    [Fact]
    public void Ignores_comments_and_blank_lines()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "; this is a comment\n[A]\n# another\nkey=value\n");
        var ini = IniFile.Read(path);
        File.Delete(path);

        Assert.Equal("value", ini["A"]["key"]);
        Assert.Single(ini);
    }
}

public class FilterFileAnalyzerTests
{
    [Fact]
    public void Counts_name_lines_and_reads_version()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path,
            "version=\"9\"\n" +
            "logging=\"no\"\n" +
            "name=\"filter A\"\nenabled=\"yes\"\n" +
            "name=\"filter B\"\nenabled=\"yes\"\n" +
            "name=\"filter C\"\nenabled=\"no\"\n");

        var stats = FilterFileAnalyzer.Read(path);
        File.Delete(path);

        Assert.Equal("9", stats.Version);
        Assert.Equal(3, stats.FilterCount);
    }

    [Fact]
    public void Missing_file_returns_zero_stats()
    {
        var stats = FilterFileAnalyzer.Read(Path.Combine(Path.GetTempPath(), "does-not-exist-" + System.Guid.NewGuid() + ".dat"));
        Assert.Null(stats.Version);
        Assert.Equal(0, stats.FilterCount);
    }
}
