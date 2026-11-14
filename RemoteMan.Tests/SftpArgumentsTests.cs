using RemoteMan.Models;
using RemoteMan.Services;
using Xunit;

namespace RemoteMan.Tests;

public class SftpArgumentsTests
{
    // --- WinSCP ---

    [Fact]
    public void WinScpSomenteHost()
    {
        var cmd = SftpArguments.Build(SftpClientKind.WinScp, " srv ", 22, null, null, null);
        Assert.Equal(["sftp://srv:22/"], cmd.Arguments);
        Assert.Empty(cmd.Warnings);
        Assert.False(cmd.KeyPassed);
    }

    [Fact]
    public void WinScpComChavePpkEJumpHost()
    {
        var cmd = SftpArguments.Build(
            SftpClientKind.WinScp, "srv", 2222, " deploy ", @" C:\keys\id.ppk ", " ops@bastion:2200 ");

        Assert.Equal(
            [
                "sftp://deploy@srv:2222/",
                @"/privatekey=C:\keys\id.ppk",
                "/rawsettings", "Tunnel=1", "TunnelHostName=bastion", "TunnelPortNumber=2200", "TunnelUserName=ops",
            ],
            cmd.Arguments);
        Assert.Empty(cmd.Warnings);
        Assert.True(cmd.KeyPassed);
    }

    [Fact]
    public void WinScpJumpHostSemUsuarioNemPorta() =>
        Assert.Equal(
            ["sftp://srv:22/", "/rawsettings", "Tunnel=1", "TunnelHostName=bastion", "TunnelPortNumber=22"],
            SftpArguments.Build(SftpClientKind.WinScp, "srv", 22, null, null, "bastion").Arguments);

    [Fact]
    public void WinScpChaveOpenSshGeraAviso()
    {
        var cmd = SftpArguments.Build(SftpClientKind.WinScp, "srv", 22, "u", @"C:\keys\id_ed25519", null);
        Assert.Equal(["sftp://u@srv:22/"], cmd.Arguments);
        Assert.Contains(cmd.Warnings, w => w.Contains(".ppk"));
        Assert.False(cmd.KeyPassed);
    }

    [Fact]
    public void WinScpVariosJumpHostsGeraAviso()
    {
        var cmd = SftpArguments.Build(SftpClientKind.WinScp, "srv", 22, null, null, "a,b");
        Assert.Equal(["sftp://srv:22/"], cmd.Arguments);
        Assert.Single(cmd.Warnings);
    }

    [Fact]
    public void WinScpJumpHostNoneEhIgnorado()
    {
        var cmd = SftpArguments.Build(SftpClientKind.WinScp, "srv", 22, null, null, "none");
        Assert.Equal(["sftp://srv:22/"], cmd.Arguments);
        Assert.Empty(cmd.Warnings);
    }

    // --- FileZilla ---

    [Fact]
    public void FileZillaSemBarraFinal()
    {
        var cmd = SftpArguments.Build(SftpClientKind.FileZilla, "srv", 2222, "deploy", null, null);
        Assert.Equal(["sftp://deploy@srv:2222"], cmd.Arguments);
        Assert.Empty(cmd.Warnings);
    }

    [Fact]
    public void FileZillaAvisaChaveEJumpHost()
    {
        var cmd = SftpArguments.Build(SftpClientKind.FileZilla, "srv", 22, "u", @"C:\keys\id", "bastion");
        Assert.Equal(["sftp://u@srv:22"], cmd.Arguments);
        Assert.Equal(2, cmd.Warnings.Count);
        Assert.False(cmd.KeyPassed);
    }

    // --- Bitvise ---

    [Fact]
    public void BitviseTodasAsOpcoes()
    {
        var cmd = SftpArguments.Build(SftpClientKind.Bitvise, " srv ", 2222, " deploy ", null, null);
        Assert.Equal(["-host=srv", "-port=2222", "-user=deploy", "-openSFTP", "-loginOnStartup"], cmd.Arguments);
        Assert.Empty(cmd.Warnings);
    }

    [Fact]
    public void BitviseSemUsuarioEPortaInvalidaUsa22() =>
        Assert.Equal(
            ["-host=srv", "-port=22", "-openSFTP", "-loginOnStartup"],
            SftpArguments.Build(SftpClientKind.Bitvise, "srv", 0, "  ", null, null).Arguments);

    [Fact]
    public void BitviseAvisaChaveEJumpHost()
    {
        var cmd = SftpArguments.Build(SftpClientKind.Bitvise, "srv", 22, null, @"C:\keys\id", "bastion");
        Assert.Equal(2, cmd.Warnings.Count);
        Assert.False(cmd.KeyPassed);
    }

    // --- Comum ---

    [Theory]
    [InlineData("srv", 22, null, "sftp://srv:22")]
    [InlineData("::1", 2222, null, "sftp://[::1]:2222")]
    [InlineData("[fe80::1]", 22, "root", "sftp://root@[fe80::1]:22")]
    [InlineData("srv", 22, "dom\\user@corp", "sftp://dom%5Cuser%40corp@srv:22")]
    public void UrlEscapaUsuarioEIPv6(string host, int port, string? user, string expected) =>
        Assert.Equal(expected, SftpArguments.Url(host, port, user, trailingSlash: false));

    [Theory]
    [InlineData("bastion", "bastion", 22, null)]
    [InlineData("ops@gw:2200", "gw", 2200, "ops")]
    [InlineData("ssh://ops@gw:2200", "gw", 2200, "ops")]
    [InlineData("[fe80::1]:2222", "fe80::1", 2222, null)]
    [InlineData("a@b@gw", "gw", 22, "a@b")]
    public void LeJumpHost(string value, string host, int port, string? user) =>
        Assert.Equal(new JumpHop(host, port, user), SftpArguments.ParseHop(value));

    [Theory]
    [InlineData("")]
    [InlineData("@gw")]
    [InlineData("gw:abc")]
    [InlineData("gw:70000")]
    [InlineData("[::1")]
    public void JumpHostInvalido(string value) => Assert.Null(SftpArguments.ParseHop(value));

    [Fact]
    public void JumpHostInvalidoGeraAvisoNoWinScp()
    {
        var cmd = SftpArguments.Build(SftpClientKind.WinScp, "srv", 22, null, null, "gw:abc");
        Assert.Equal(["sftp://srv:22/"], cmd.Arguments);
        Assert.Single(cmd.Warnings);
    }

    [Fact]
    public void EscolhePreferidoQuandoInstalado() =>
        Assert.Equal(
            SftpClientKind.Bitvise,
            SftpArguments.Choose(SftpClientKind.Bitvise, [SftpClientKind.FileZilla, SftpClientKind.Bitvise]));

    [Fact]
    public void PreferidoAusenteUsaPrimeiroNaOrdemPadrao() =>
        Assert.Equal(
            SftpClientKind.FileZilla,
            SftpArguments.Choose(SftpClientKind.WinScp, [SftpClientKind.Bitvise, SftpClientKind.FileZilla]));

    [Fact]
    public void NenhumInstalado() => Assert.Null(SftpArguments.Choose(null, []));
}
