using RemoteMan.Models;
using RemoteMan.Services.Importers;
using Xunit;

namespace RemoteMan.Tests;

public class BackupFileTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 30, 0);

    private static readonly ImportedConnection Rdp = new()
    {
        Protocol = ConnectionProtocol.Rdp, Name = "Servidor 01", Host = "srv01", Port = 3390,
        Username = "joao", Domain = "EMPRESA", Group = "Produção", Notes = "linha 1\nlinha 2", IsFavorite = true,
        UseWindowsCredentials = false, FullScreen = false, DesktopWidth = 1280, DesktopHeight = 720,
        MultiMonitor = true, RedirectClipboard = false, RedirectDrives = true, AdminSession = true,
        GatewayHost = "gw", GatewayBypassLocal = true, GatewayUseSameCredentials = false,
    };

    private static readonly ImportedConnection Ssh = new()
    {
        Protocol = ConnectionProtocol.Ssh, Name = "web", Host = "10.0.0.5", Port = 22, Username = "deploy",
        KeyFile = @"C:\keys\id", JumpHost = "bastion", Warning = "não deve ir para o arquivo",
    };

    [Fact]
    public void IdaEVoltaPreservaTudoMenosAvisos()
    {
        var json = BackupFile.Serialize([Rdp, Ssh], Now);
        var result = BackupFile.Parse(json);

        Assert.Empty(result.Errors);
        Assert.Equal([Rdp, Ssh with { Warning = null }], result.Connections);
        Assert.DoesNotContain("não deve ir", json);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("""{"format":"outro","version":1,"exportedAt":"2026-01-01T00:00:00","connections":[]}""")]
    [InlineData("""{"format":"remoteman-backup","version":99,"exportedAt":"2026-01-01T00:00:00","connections":[]}""")]
    public void RejeitaArquivosInvalidos(string json)
    {
        var result = BackupFile.Parse(json);
        Assert.Empty(result.Connections);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void IgnoraConexoesComEnderecoInvalido()
    {
        var json = BackupFile.Serialize([Rdp, Ssh with { Host = " " }, Ssh with { Name = "porta", Port = 70000 }], Now);
        var result = BackupFile.Parse(json);
        Assert.Equal("Servidor 01", Assert.Single(result.Connections).Name);
        Assert.Equal(2, result.Errors.Count);
    }
}

public class SshConfigWriterTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 30, 0);
    private static readonly string Home = Path.Combine("C:", "Users", "joao");

    private static ImportedConnection Ssh(string name, string host, int port = 22, string user = "", string key = "", string jump = "") =>
        new() { Protocol = ConnectionProtocol.Ssh, Name = name, Host = host, Port = port, Username = user, KeyFile = key, JumpHost = jump };

    [Fact]
    public void GeraBlocosEIgnoraRdp()
    {
        var text = SshConfigWriter.Write(
        [
            Ssh("Web 01", "10.0.0.5", 2222, "deploy", @"C:\Minhas Chaves\id", "bastion"),
            Ssh("db", "db.local"),
            new ImportedConnection { Protocol = ConnectionProtocol.Rdp, Name = "rdp", Host = "srv", Port = 3389 },
        ], Now);

        Assert.Contains("Host Web-01\n    HostName 10.0.0.5\n    User deploy\n    Port 2222\n    IdentityFile \"C:\\Minhas Chaves\\id\"\n    ProxyJump bastion\n", text);
        Assert.Contains("Host db\n    HostName db.local\n", text);
        Assert.DoesNotContain("Port 22\n", text);
        Assert.DoesNotContain("rdp", text.Replace("RemoteMan", ""));
    }

    [Fact]
    public void ArquivoGeradoPodeSerImportadoDeVolta()
    {
        var original = new[] { Ssh("Web 01", "10.0.0.5", 2222, "deploy", jump: "bastion,user@gw:2200"), Ssh("db", "db.local") };
        var result = SshConfigParser.Parse(SshConfigWriter.Write(original, Now), Home);

        Assert.Empty(result.Errors);
        Assert.Equal(["Web-01", "db"], result.Connections.Select(c => c.Name));
        Assert.Equal(("10.0.0.5", 2222, "deploy", "bastion,user@gw:2200"),
            (result.Connections[0].Host, result.Connections[0].Port, result.Connections[0].Username, result.Connections[0].JumpHost));
    }

    [Theory]
    [InlineData("Web 01", "Web-01")]
    [InlineData("  prod*web?  ", "prodweb")]
    [InlineData("***", "10.0.0.1")] // sobra nada: usa o host
    public void Alias(string name, string expected) =>
        Assert.Equal(expected, SshConfigWriter.UniqueAlias(name, "10.0.0.1", new HashSet<string>()));

    [Fact]
    public void AliasRepetidoGanhaSufixo()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["web", "WEB-2", "web-3"], new[] { "web", "WEB", "web" }.Select(n => SshConfigWriter.UniqueAlias(n, "h", used)));
    }
}

public class ExportNamesTests
{
    [Theory]
    [InlineData("Servidor 01", "Servidor 01.rdp")]
    [InlineData("srv: web/01", "srv_ web_01.rdp")]
    [InlineData("  nome.  ", "nome.rdp")]
    [InlineData("???", "___.rdp")]
    [InlineData("", "conexao.rdp")]
    public void FileName(string name, string expected) =>
        Assert.Equal(expected, ExportNames.FileName(name, ".rdp", new HashSet<string>()));

    [Fact]
    public void NomeRepetidoGanhaNumero()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "srv.rdp" };
        Assert.Equal("srv (2).rdp", ExportNames.FileName("srv", ".rdp", used));
        Assert.Equal("SRV (3).rdp", ExportNames.FileName("SRV", ".rdp", used));
    }
}
