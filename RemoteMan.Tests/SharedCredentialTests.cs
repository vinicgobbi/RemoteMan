using RemoteMan.Models;
using Xunit;

namespace RemoteMan.Tests;

public class SharedCredentialTests
{
    [Theory]
    [InlineData("admin", "", "admin")]
    [InlineData("admin", " EMPRESA ", @"EMPRESA\admin")]
    [InlineData("admin@empresa.com", "", "admin@empresa.com")]
    public void FullUsername(string user, string domain, string expected) =>
        Assert.Equal(expected, new SharedCredential { Username = user, Domain = domain }.FullUsername);

    [Theory]
    [InlineData("Admin do domínio", "admin", "EMPRESA", @"Admin do domínio (EMPRESA\admin)")]
    [InlineData("Sem usuário", "", "", "Sem usuário")]
    public void DisplayText(string name, string user, string domain, string expected) =>
        Assert.Equal(expected, new SharedCredential { Name = name, Username = user, Domain = domain }.DisplayText);

    [Fact]
    public void CadaCredencialTemIdProprio() =>
        Assert.NotEqual(new SharedCredential().Id, new SharedCredential().Id);
}
