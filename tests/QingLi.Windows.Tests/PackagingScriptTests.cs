namespace QingLi.Windows.Tests;

public sealed class PackagingScriptTests
{
    [Fact]
    public void PortablePackagingCanSucceedWithoutMsixAndRequiresOfflineAssets()
    {
        var script = File.ReadAllText(GetScriptPath());

        Assert.Contains("[switch]$PortableOnly", script);
        Assert.Contains("Assets\\History\\history-today.zh-CN.json", script);
        Assert.Contains("Assets\\Holidays\\cn-2026.json", script);
        Assert.Contains("if ($PortableOnly)", script);
        Assert.Contains("QingLi.Recovery", script);
        Assert.Contains("function Test-PortableArchive", script);
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("Test-PortableArchive -ArchivePath $portableZip -SourceDirectory $publishDir", script);
        Assert.DoesNotContain("[IO.Path]::GetRelativePath", script);
        Assert.Contains(".Substring($sourcePrefix.Length)", script);
    }

    private static string GetScriptPath() => Path.Combine(
        GetRepositoryRoot(), "scripts", "package.ps1");

    private static string GetRepositoryRoot() => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
