using RemoteMan.Models;
using RemoteMan.Services.Importers;
using Xunit;

namespace RemoteMan.Tests;

public class RdpFileParserTests
{
    [Fact]
    public void LeArquivoDoMstsc()
    {
        const string rdp = """
            screen mode id:i:1
            use multimon:i:1
            desktopwidth:i:1600
            desktopheight:i:900
            full address:s:srv01.empresa.local:3390
            redirectclipboard:i:0
            drivestoredirect:s:*
            administrative session:i:1
            username:s:EMPRESA\joao
            password 51:b:01000000D08C9DDF0115D1118C7A00C04FC297EB
            """;

        Assert.True(RdpFileParser.TryParse("Servidor 01", rdp.Replace("\n", "\r\n"), out var c, out var error), error);
        Assert.Equal(ConnectionProtocol.Rdp, c.Protocol);
        Assert.Equal("Servidor 01", c.Name);
        Assert.Equal("srv01.empresa.local", c.Host);
        Assert.Equal(3390, c.Port);
        Assert.Equal("EMPRESA", c.Domain);
        Assert.Equal("joao", c.Username);
        Assert.False(c.FullScreen);
        Assert.Equal(1600, c.DesktopWidth);
        Assert.Equal(900, c.DesktopHeight);
        Assert.True(c.MultiMonitor);
        Assert.False(c.RedirectClipboard);
        Assert.True(c.RedirectDrives);
        Assert.True(c.AdminSession);
        Assert.Null(c.Warning);
    }

    [Fact]
    public void OpcoesAusentesFicamNulas()
    {
        Assert.True(RdpFileParser.TryParse("x", "full address:s:10.0.0.5", out var c, out _));
        Assert.Equal(3389, c.Port);
        Assert.Equal("", c.Username);
        Assert.Null(c.FullScreen);
        Assert.Null(c.MultiMonitor);
        Assert.Null(c.RedirectDrives);
        Assert.Null(c.AdminSession);
    }

    [Fact]
    public void UsaServerPortQuandoEnderecoNaoTemPorta()
    {
        Assert.True(RdpFileParser.TryParse("x", "full address:s:srv\nserver port:i:4000", out var c, out _));
        Assert.Equal(4000, c.Port);
    }

    [Fact]
    public void AvisaSobreGateway()
    {
        const string rdp = "full address:s:srv\ngatewayhostname:s:gw.empresa.com\ngatewayusagemethod:i:1";
        Assert.True(RdpFileParser.TryParse("x", rdp, out var c, out _));
        Assert.Contains("Gateway", c.Warning);
    }

    [Theory]
    [InlineData("screen mode id:i:2")]
    [InlineData("full address:s:")]
    [InlineData("full address:s:srv 01")]
    public void RejeitaArquivoSemEnderecoValido(string content)
    {
        Assert.False(RdpFileParser.TryParse("x", content, out _, out var error));
        Assert.StartsWith("x:", error);
    }
}

public class SshConfigParserTests
{
    private static readonly string Home = Path.Combine("C:", "Users", "joao");

    [Fact]
    public void LeHostsComPadroesDeHostCuringa()
    {
        const string config = """
            # comentário
            Host web web-alias
                HostName 192.168.0.10
                User deploy
                Port 2222
                IdentityFile ~/.ssh/id_web

            Host db
              HostName=db.interno
              User = postgres

            Host *.lab !excluido.lab
              User lab

            Host *
              User root
              IdentityFile ~/.ssh/id_ed25519
            """;

        var result = SshConfigParser.Parse(config, Home);

        Assert.Empty(result.Errors);
        Assert.Equal(["web", "web-alias", "db"], result.Connections.Select(c => c.Name));

        var web = result.Connections[0];
        Assert.Equal(ConnectionProtocol.Ssh, web.Protocol);
        Assert.Equal("192.168.0.10", web.Host);
        Assert.Equal(2222, web.Port);
        Assert.Equal("deploy", web.Username); // primeiro valor vence o "Host *"
        Assert.Equal(Path.Combine(Home, ".ssh", "id_web"), web.KeyFile);

        var db = result.Connections[2];
        Assert.Equal("db.interno", db.Host);
        Assert.Equal(22, db.Port);
        Assert.Equal("postgres", db.Username);
        Assert.Equal(Path.Combine(Home, ".ssh", "id_ed25519"), db.KeyFile); // veio do "Host *"
    }

    [Fact]
    public void SemHostNameUsaOAlias()
    {
        var result = SshConfigParser.Parse("Host meu-servidor\n  User admin", Home);
        Assert.Equal("meu-servidor", Assert.Single(result.Connections).Host);
    }

    [Fact]
    public void OpcoesAntesDoPrimeiroHostValemParaTodosEVencem()
    {
        // como no ssh: são lidas primeiro e o primeiro valor obtido é o que vale
        var result = SshConfigParser.Parse("User global\nHost a\nHost b\n  User b-user", Home);
        Assert.Equal(["global", "global"], result.Connections.Select(c => c.Username));
    }

    [Fact]
    public void IgnoraMatchEAvisaInclude()
    {
        const string config = """
            Include ~/.ssh/config.d/*
            Host a
              User a
            Match host a
              User do-match
            """;
        var result = SshConfigParser.Parse(config, Home);
        Assert.Equal("a", Assert.Single(result.Connections).Username);
        Assert.Contains("Include", Assert.Single(result.Errors));
    }

    [Fact]
    public void AvisaProxyJumpERejeitaPortaInvalida()
    {
        var result = SshConfigParser.Parse("Host a\n  ProxyJump bastion\nHost b\n  Port 99999", Home);
        Assert.Contains("ProxyJump", Assert.Single(result.Connections).Warning);
        Assert.Contains("b:", Assert.Single(result.Errors));
    }

    [Fact]
    public void ArquivoSoComCuringasNaoGeraConexoes()
    {
        var result = SshConfigParser.Parse("Host *\n  User root", Home);
        Assert.Empty(result.Connections);
    }
}

public class PuttySessionParserTests
{
    [Fact]
    public void LeSessaoSsh()
    {
        var c = PuttySessionParser.Parse("Servidor%20Web", new Dictionary<string, object?>
        {
            ["Protocol"] = "ssh",
            ["HostName"] = "web.empresa.com",
            ["PortNumber"] = 2200,
            ["UserName"] = "deploy",
            ["PublicKeyFile"] = @"C:\keys\web.ppk",
        });

        Assert.NotNull(c);
        Assert.Equal("Servidor Web", c.Name);
        Assert.Equal("web.empresa.com", c.Host);
        Assert.Equal(2200, c.Port);
        Assert.Equal("deploy", c.Username);
        Assert.Equal("", c.KeyFile);
        Assert.Contains(".ppk", c.Warning);
    }

    [Fact]
    public void UsuarioPodeVirNoHost()
    {
        var c = PuttySessionParser.Parse("s", new Dictionary<string, object?>
        {
            ["Protocol"] = "ssh",
            ["HostName"] = "root@10.0.0.1",
        });
        Assert.NotNull(c);
        Assert.Equal("10.0.0.1", c.Host);
        Assert.Equal("root", c.Username);
        Assert.Equal(22, c.Port);
        Assert.Null(c.Warning);
    }

    [Theory]
    [InlineData("telnet", "srv")]
    [InlineData("serial", "COM1")]
    [InlineData("ssh", "")]
    public void IgnoraSessoesQueNaoSaoSshOuSemHost(string protocol, string host)
    {
        Assert.Null(PuttySessionParser.Parse("Default%20Settings", new Dictionary<string, object?>
        {
            ["Protocol"] = protocol,
            ["HostName"] = host,
        }));
    }
}
