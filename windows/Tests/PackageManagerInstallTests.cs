using QuadClicker.Core;
using Xunit;

namespace QuadClicker.Tests;

public sealed class PackageManagerInstallTests
{
    [Theory]
    [InlineData(@"C:\Users\me\scoop\apps\quadclicker\current\QuadClicker.exe")]
    [InlineData(@"C:\ProgramData\chocolatey\lib\quadclicker\tools\QuadClicker.exe")]
    [InlineData(@"C:\Users\me\AppData\Local\Microsoft\WinGet\Packages\X.QuadClicker_abc\QuadClicker.exe")]
    [InlineData(@"C:\Users\me\AppData\Local\Microsoft\WinGet\Links\QuadClicker.exe")]
    [InlineData("C:/Users/me/SCOOP/apps/quadclicker/0.2.0/QuadClicker.exe")]
    public void ManagerPaths_AreManaged(string path) => Assert.True(PackageManagerInstall.IsManaged(path));

    [Theory]
    [InlineData(@"C:\Users\me\Downloads\QuadClicker.exe")]
    [InlineData(@"D:\tools\QuadClicker.exe")]
    [InlineData("")]
    [InlineData(null)]
    public void DirectDownloads_AreNotManaged(string? path) => Assert.False(PackageManagerInstall.IsManaged(path));
}
