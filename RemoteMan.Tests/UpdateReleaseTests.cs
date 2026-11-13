using RemoteMan.Services;
using Xunit;

namespace RemoteMan.Tests;

public class UpdateReleaseTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("V1.10.3", "1.10.3")]
    [InlineData("1.2", "1.2.0")]
    [InlineData("v2.0.0-beta.1", "2.0.0")]
    [InlineData("1.0.0+abc", "1.0.0")]
    public void ParseVersion_Validas(string tag, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateRelease.ParseVersion(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v")]
    public void ParseVersion_Invalidas(string? tag) =>
        Assert.Null(UpdateRelease.ParseVersion(tag));

    [Theory]
    [InlineData("0.2.0", "0.3.0", true)]
    [InlineData("0.2.0", "0.2.1", true)]
    [InlineData("0.9.0", "0.10.0", true)] // comparação numérica, não de texto
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.3.0", "0.2.9", false)]
    [InlineData("0.2.0.0", "0.2.0", false)] // versão do assembly tem 4 partes
    public void IsNewer(string current, string latest, bool expected) =>
        Assert.Equal(expected, UpdateRelease.IsNewer(Version.Parse(current), Version.Parse(latest)));

    private static readonly ReleaseAsset[] Assets =
    [
        new("RemoteMan-0.3.0-arm64-portable.zip", "https://x/arm64.zip"),
        new("RemoteMan-0.3.0-x64-portable.zip", "https://x/x64.zip"),
        new("RemoteMan-Setup-0.3.0-arm64.exe", "https://x/arm64.exe"),
        new("RemoteMan-Setup-0.3.0-x64.exe", "https://x/x64.exe"),
    ];

    [Theory]
    [InlineData("x64", true, "https://x/x64.exe")]
    [InlineData("arm64", true, "https://x/arm64.exe")]
    [InlineData("x64", false, "https://x/x64.zip")]
    [InlineData("arm64", false, "https://x/arm64.zip")]
    public void PickAsset_InstaladorOuPortatil(string arch, bool installed, string expected) =>
        Assert.Equal(expected, UpdateRelease.PickAsset(Assets, arch, installed)?.DownloadUrl);

    [Fact]
    public void PickAsset_UsaOOutroFormatoSeOPreferidoNaoExistir()
    {
        ReleaseAsset[] onlyZip = [new("RemoteMan-0.1.0-win-x64.zip", "z"), new("RemoteMan-0.3.0-x64-portable.zip", "p")];
        Assert.Equal("p", UpdateRelease.PickAsset(onlyZip, "x64", installed: true)?.DownloadUrl);
        Assert.Null(UpdateRelease.PickAsset([], "x64", installed: true));
    }
}
