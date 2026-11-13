using RemoteMan.Models;
using RemoteMan.Services;
using RemoteMan.Services.Importers;
using Xunit;

namespace RemoteMan.Tests;

public class SshTunnelsTests
{
    [Fact]
    public void LeOsTresTiposIgnorandoVaziasEComentarios()
    {
        const string text = """
            # banco
            L 5432:db.interno:5432
            -R 9000:localhost:3000

            d 1080
            L 127.0.0.1:8080:[fe80::1]:80
            """;

        var (tunnels, errors) = SshTunnels.Parse(text.Replace("\n", "\r\n"));

        Assert.Empty(errors);
        Assert.Equal(
        [
            new SshTunnel(TunnelKind.Local, null, 5432, "db.interno", 5432),
            new SshTunnel(TunnelKind.Remote, null, 9000, "localhost", 3000),
            new SshTunnel(TunnelKind.Dynamic, null, 1080, null, null),
            new SshTunnel(TunnelKind.Local, "127.0.0.1", 8080, "fe80::1", 80),
        ], tunnels);
        Assert.Equal("127.0.0.1:8080:[fe80::1]:80", tunnels[3].Spec);
    }

    [Theory]
    [InlineData("X 8080:localhost:80", "Linha 1: use L")]
    [InlineData("L 8080", "Linha 1: use L porta:host:porta")]
    [InlineData("L 0:localhost:80", "porta inválida (0)")]
    [InlineData("L 8080::80", "informe o host")]
    [InlineData("L 8080:localhost:99999", "porta de destino inválida")]
    [InlineData("D abc", "porta inválida (abc)")]
    [InlineData("D", "use D porta")]
    public void InformaErrosComNumeroDaLinha(string line, string expected)
    {
        var (tunnels, errors) = SshTunnels.Parse(line);
        Assert.Empty(tunnels);
        Assert.Contains(expected, Assert.Single(errors));
    }

    [Fact]
    public void LinhasValidasContinuamMesmoComErros()
    {
        var (tunnels, errors) = SshTunnels.Parse("L 8080:localhost:80\nlixo\nD 1080");
        Assert.Equal(2, tunnels.Count);
        Assert.StartsWith("Linha 2:", Assert.Single(errors));
    }

    [Fact]
    public void ArgumentosDoSsh() =>
        Assert.Equal(
            ["-L", "8080:localhost:80", "-D", "1080", "srv"],
            SshArguments.Build("srv", 22, null, null, null, SshTunnels.Parse("L 8080:localhost:80\nD 1080").Tunnels));

    [Theory]
    [InlineData(TunnelKind.Local, "8080 localhost:80", "LocalForward 8080 localhost:80")]
    [InlineData(TunnelKind.Local, "127.0.0.1:8080 db:5432", "LocalForward 127.0.0.1:8080 db:5432")]
    [InlineData(TunnelKind.Remote, "9000 localhost:3000", "RemoteForward 9000 localhost:3000")]
    [InlineData(TunnelKind.Dynamic, "1080", "DynamicForward 1080")]
    public void OpcoesDoSshConfigIdaEVolta(TunnelKind kind, string value, string configLine)
    {
        Assert.True(SshTunnels.TryParseConfigForward(kind, value, out var tunnel));
        Assert.Equal(configLine, SshTunnels.ToConfigLine(tunnel));
    }

    [Fact]
    public void ImportaForwardsDoSshConfigAcumulandoBlocos()
    {
        const string config = """
            Host web
              LocalForward 8080 localhost:80
              LocalForward 8443 localhost:443
            Host *
              DynamicForward 1080
              LocalForward invalido
            """;

        var result = SshConfigParser.Parse(config, "C:\\Users\\x");
        Assert.Equal("L 8080:localhost:80\nL 8443:localhost:443\nD 1080", Assert.Single(result.Connections).Tunnels);
        Assert.Contains("LocalForward invalido", Assert.Single(result.Errors));
    }

    [Fact]
    public void ExportaForwardsNoSshConfig()
    {
        var text = SshConfigWriter.Write(
        [
            new ImportedConnection
            {
                Protocol = ConnectionProtocol.Ssh, Name = "web", Host = "srv", Port = 22,
                Tunnels = "L 8080:localhost:80\nlixo\nD 1080",
            },
        ], DateTime.Now);

        Assert.Contains("    LocalForward 8080 localhost:80\n    DynamicForward 1080\n", text);
        Assert.Equal("L 8080:localhost:80\nD 1080", Assert.Single(SshConfigParser.Parse(text, "C:\\x").Connections).Tunnels);
    }
}
