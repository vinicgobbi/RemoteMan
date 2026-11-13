using RemoteMan.Models;
using Xunit;

namespace RemoteMan.Tests;

public class ConnectionAddressTests
{
    [Theory]
    [InlineData("servidor.empresa.local", "servidor.empresa.local", null, null)]
    [InlineData("  192.168.0.10  ", "192.168.0.10", null, null)]
    [InlineData("192.168.0.10:3390", "192.168.0.10", 3390, null)]
    [InlineData("root@srv01", "srv01", null, "root")]
    [InlineData("admin@srv01:2222", "srv01", 2222, "admin")]
    [InlineData("ssh://deploy@srv01:2200/", "srv01", 2200, "deploy")]
    [InlineData("rdp://srv01", "srv01", null, null)]
    [InlineData("fe80::1", "fe80::1", null, null)]
    [InlineData("[fe80::1]:3390", "fe80::1", 3390, null)]
    [InlineData("user@mail.com@srv01", "srv01", null, "user@mail.com")]
    [InlineData("@srv01", "srv01", null, null)]
    public void TryParse_EnderecosValidos(string text, string host, int? port, string? user)
    {
        Assert.True(ConnectionAddress.TryParse(text, out var address, out var error), error);
        Assert.Equal(new ConnectionAddress(host, port, user), address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData(":3389")]
    [InlineData("srv 01")]
    [InlineData("srv01:0")]
    [InlineData("srv01:65536")]
    [InlineData("srv01:abc")]
    [InlineData("[fe80::1")]
    [InlineData("[fe80::1]x")]
    public void TryParse_EnderecosInvalidos(string? text)
    {
        Assert.False(ConnectionAddress.TryParse(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData(ConnectionProtocol.Rdp, 3389)]
    [InlineData(ConnectionProtocol.Ssh, 22)]
    public void DefaultPort_PorProtocolo(ConnectionProtocol protocol, int expected) =>
        Assert.Equal(expected, ConnectionAddress.DefaultPort(protocol));

    [Theory]
    [InlineData(@"EMPRESA\joao", "EMPRESA", "joao")]
    [InlineData("joao", "", "joao")]
    [InlineData("joao@empresa.com", "", "joao@empresa.com")]
    [InlineData(@"\joao", "", @"\joao")]
    public void SplitDomainUser(string text, string domain, string user) =>
        Assert.Equal((domain, user), ConnectionAddress.SplitDomainUser(text));
}
