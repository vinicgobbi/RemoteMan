using RemoteMan.Services;
using Xunit;

namespace RemoteMan.Tests;

public class SshArgumentsTests
{
    [Fact]
    public void SomenteHost() =>
        Assert.Equal(["srv"], SshArguments.Build(" srv ", 22, null, null, null));

    [Fact]
    public void TodasAsOpcoes() =>
        Assert.Equal(
            ["-p", "2222", "-i", @"C:\keys\id", "-J", "bastion,user@gw:2200", "deploy@srv"],
            SshArguments.Build("srv", 2222, " deploy ", @" C:\keys\id ", " bastion , user@gw:2200 "));

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData("none", "")]
    [InlineData("NONE", "")]
    [InlineData("bastion", "bastion")]
    [InlineData("a, ,b,", "a,b")]
    public void NormalizaJumpHost(string? value, string expected) =>
        Assert.Equal(expected, SshArguments.NormalizeJumpHost(value));
}
