using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Linha de comando de um cliente SFTP e o que não pôde ser repassado a ele.</summary>
/// <param name="Arguments">Argumentos do executável do cliente (nunca contêm senha).</param>
/// <param name="Warnings">Avisos curtos (ex.: chave ou jump host não suportados pelo cliente).</param>
/// <param name="KeyPassed">Se a chave privada foi repassada ao cliente.</param>
public sealed record SftpCommand(List<string> Arguments, List<string> Warnings, bool KeyPassed);

/// <summary>Servidor intermediário (jump host) no formato do ssh -J: [user@]host[:porta].</summary>
public sealed record JumpHop(string Host, int Port, string? User);

/// <summary>Monta os argumentos dos clientes SFTP (WinSCP, FileZilla, Bitvise) para uma conexão SSH.</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente. Senha nunca vai na linha de comando.</remarks>
public static class SftpArguments
{
    private const int DefaultPort = 22;

    public static SftpCommand Build(
        SftpClientKind client, string host, int port, string? username, string? keyFile, string? jumpHost)
    {
        port = port is > 0 and <= 65535 ? port : DefaultPort;
        username = string.IsNullOrWhiteSpace(username) ? null : username.Trim();
        keyFile = string.IsNullOrWhiteSpace(keyFile) ? null : keyFile.Trim();
        var jump = SshArguments.NormalizeJumpHost(jumpHost);

        return client switch
        {
            SftpClientKind.WinScp => WinScp(host, port, username, keyFile, jump),
            SftpClientKind.FileZilla => FileZilla(host, port, username, keyFile, jump),
            SftpClientKind.Bitvise => Bitvise(host, port, username, keyFile, jump),
            _ => throw new ArgumentOutOfRangeException(nameof(client), client, null),
        };
    }

    /// <summary>
    /// Cliente a usar: o preferido, se estiver instalado; senão o primeiro instalado na ordem do enum; null se nenhum.
    /// </summary>
    public static SftpClientKind? Choose(SftpClientKind? preferred, IEnumerable<SftpClientKind> installed)
    {
        var set = installed.ToHashSet();
        if (preferred is { } p && set.Contains(p))
            return p;
        return Enum.GetValues<SftpClientKind>().Where(set.Contains).Select(k => (SftpClientKind?)k).FirstOrDefault();
    }

    // WinSCP: sftp://usuario@host:porta/ /privatekey=chave.ppk /rawsettings Tunnel=1 ...
    private static SftpCommand WinScp(string host, int port, string? username, string? keyFile, string jump)
    {
        var args = new List<string> { Url(host, port, username, trailingSlash: true) };
        var warnings = new List<string>();
        var keyPassed = false;

        if (keyFile is not null)
        {
            if (keyFile.EndsWith(".ppk", StringComparison.OrdinalIgnoreCase))
            {
                args.Add($"/privatekey={keyFile}");
                keyPassed = true;
            }
            else
            {
                warnings.Add("o WinSCP só aceita chaves .ppk (converta a chave no PuTTYgen ou no próprio WinSCP)");
            }
        }

        if (jump.Length > 0)
        {
            var hops = jump.Split(',');
            if (hops.Length > 1)
            {
                warnings.Add("o WinSCP aceita só um servidor intermediário; o jump host não foi usado");
            }
            else if (ParseHop(hops[0]) is not { } hop)
            {
                warnings.Add($"jump host inválido (\"{hops[0]}\"); não foi usado");
            }
            else
            {
                // /rawsettings consome os argumentos seguintes: precisa ser o último
                args.Add("/rawsettings");
                args.Add("Tunnel=1");
                args.Add($"TunnelHostName={hop.Host}");
                args.Add($"TunnelPortNumber={hop.Port}");
                if (hop.User is not null)
                    args.Add($"TunnelUserName={hop.User}");
            }
        }

        return new(args, warnings, keyPassed);
    }

    // FileZilla: sftp://usuario@host:porta (sem barra final, para abrir na pasta inicial do usuário)
    private static SftpCommand FileZilla(string host, int port, string? username, string? keyFile, string jump)
    {
        var warnings = new List<string>();
        if (keyFile is not null)
            warnings.Add("o FileZilla não recebe a chave por parâmetro (adicione-a em Editar › Configurações › SFTP)");
        if (jump.Length > 0)
            warnings.Add("o FileZilla não suporta jump host");

        return new([Url(host, port, username, trailingSlash: false)], warnings, false);
    }

    // Bitvise: BvSsh.exe -host= -port= -user= -openSFTP -loginOnStartup
    private static SftpCommand Bitvise(string host, int port, string? username, string? keyFile, string jump)
    {
        var args = new List<string> { $"-host={host.Trim()}", $"-port={port}" };
        if (username is not null)
            args.Add($"-user={username}");
        args.Add("-openSFTP");
        args.Add("-loginOnStartup");

        var warnings = new List<string>();
        if (keyFile is not null)
            warnings.Add("o Bitvise não recebe a chave por parâmetro (importe-a no gerenciador de chaves do Bitvise)");
        if (jump.Length > 0)
            warnings.Add("o Bitvise não recebe jump host por parâmetro");

        return new(args, warnings, false);
    }

    /// <summary>URL sftp:// com usuário escapado e IPv6 entre colchetes.</summary>
    public static string Url(string host, int port, string? username, bool trailingSlash)
    {
        host = host.Trim();
        if (host.Contains(':') && !host.StartsWith('['))
            host = $"[{host}]";

        var user = string.IsNullOrWhiteSpace(username) ? string.Empty : Uri.EscapeDataString(username.Trim()) + "@";
        return $"sftp://{user}{host}:{port}{(trailingSlash ? "/" : string.Empty)}";
    }

    /// <summary>Lê um servidor intermediário ("bastion", "user@gw:2222", "[::1]:22", "ssh://gw"); null se inválido.</summary>
    public static JumpHop? ParseHop(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
            text = text[6..].TrimEnd('/');

        string? user = null;
        var at = text.LastIndexOf('@');
        if (at >= 0)
        {
            user = text[..at];
            text = text[(at + 1)..];
            if (user.Length == 0)
                return null;
        }

        string host;
        string? portText = null;
        if (text.StartsWith('['))
        {
            var close = text.IndexOf(']');
            if (close < 0)
                return null;
            host = text[1..close];
            var rest = text[(close + 1)..];
            if (rest.Length > 0)
            {
                if (!rest.StartsWith(':'))
                    return null;
                portText = rest[1..];
            }
        }
        else if (text.Count(ch => ch == ':') == 1)
        {
            var colon = text.IndexOf(':');
            host = text[..colon];
            portText = text[(colon + 1)..];
        }
        else
        {
            host = text; // sem porta (ou IPv6 sem colchetes)
        }

        var port = DefaultPort;
        if (portText is not null && (!int.TryParse(portText, out port) || port is < 1 or > 65535))
            return null;

        return host.Length == 0 || host.Any(char.IsWhiteSpace) ? null : new(host, port, user);
    }
}
