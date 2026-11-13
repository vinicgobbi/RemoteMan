using RemoteMan.Services;
using RemoteMan.Services.Importers;
using Xunit;

namespace RemoteMan.Tests;

public class RdpFileBuilderTests
{
    private static string[] Lines(RdpFileOptions o) =>
        RdpFileBuilder.Build(o).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void PadraoSemGateway()
    {
        var lines = Lines(new RdpFileOptions { Host = " srv01 " });
        Assert.Contains("full address:s:srv01", lines);
        Assert.Contains("screen mode id:i:2", lines);
        Assert.Contains("gatewayusagemethod:i:0", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("gatewayhostname"));
        Assert.DoesNotContain(lines, l => l.StartsWith("username"));
        Assert.DoesNotContain(lines, l => l.StartsWith("desktopwidth"));
    }

    [Fact]
    public void JanelaPortaEUsuario()
    {
        var lines = Lines(new RdpFileOptions
        {
            Host = "srv01", Port = 3390, Username = @"EMPRESA\joao",
            FullScreen = false, DesktopWidth = 1280, DesktopHeight = 720, RedirectDrives = true,
        });
        Assert.Contains("full address:s:srv01:3390", lines);
        Assert.Contains(@"username:s:EMPRESA\joao", lines);
        Assert.Contains("screen mode id:i:1", lines);
        Assert.Contains("desktopwidth:i:1280", lines);
        Assert.Contains("desktopheight:i:720", lines);
        Assert.Contains("drivestoredirect:s:*", lines);
    }

    [Theory]
    [InlineData(false, true, "gatewayusagemethod:i:1", "promptcredentialonce:i:1")]
    [InlineData(true, false, "gatewayusagemethod:i:2", "promptcredentialonce:i:0")]
    public void Gateway(bool bypassLocal, bool sameCredentials, string method, string prompt)
    {
        var lines = Lines(new RdpFileOptions
        {
            Host = "srv01", GatewayHost = " gw.empresa.com ",
            GatewayBypassLocal = bypassLocal, GatewayUseSameCredentials = sameCredentials,
        });
        Assert.Contains("gatewayhostname:s:gw.empresa.com", lines);
        Assert.Contains(method, lines);
        Assert.Contains(prompt, lines);
        Assert.Contains("gatewayprofileusagemethod:i:1", lines);
    }

    [Fact]
    public void ArquivoGeradoPodeSerImportadoDeVolta()
    {
        var rdp = RdpFileBuilder.Build(new RdpFileOptions
        {
            Host = "srv01", Port = 3390, Username = @"EMPRESA\joao", FullScreen = false,
            DesktopWidth = 1280, DesktopHeight = 720, GatewayHost = "gw", GatewayBypassLocal = true,
        });

        Assert.True(RdpFileParser.TryParse("srv01", rdp, out var c, out var error), error);
        Assert.Equal(("srv01", 3390, "EMPRESA", "joao"), (c.Host, c.Port, c.Domain, c.Username));
        Assert.Equal((false, 1280, 720), (c.FullScreen, c.DesktopWidth, c.DesktopHeight));
        Assert.Equal(("gw", true), (c.GatewayHost, c.GatewayBypassLocal));
    }
}
