namespace RemoteMan.Models;

/// <summary>
/// Endereço digitado pelo usuário no diálogo de nova conexão. Aceita "host", "host:porta",
/// "[ipv6]:porta", "usuario@host" e prefixos como "ssh://" ou "rdp://".
/// </summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public sealed record ConnectionAddress(string Host, int? Port, string? User)
{
    public const int DefaultRdpPort = 3389;
    public const int DefaultSshPort = 22;

    public static int DefaultPort(ConnectionProtocol protocol) =>
        protocol == ConnectionProtocol.Rdp ? DefaultRdpPort : DefaultSshPort;

    public static bool TryParse(string? text, out ConnectionAddress result, out string error)
    {
        result = null!;
        error = string.Empty;

        var s = text?.Trim() ?? string.Empty;
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            s = s[(scheme + 3)..];
        s = s.TrimEnd('/');

        string? user = null;
        var at = s.LastIndexOf('@');
        if (at >= 0)
        {
            user = s[..at];
            s = s[(at + 1)..];
            if (user.Length == 0)
                user = null;
        }

        string host;
        string? portText = null;
        if (s.StartsWith('['))
        {
            // IPv6 com porta: [fe80::1]:3390
            var close = s.IndexOf(']');
            if (close < 0)
            {
                error = "Endereço IPv6 incompleto: falta o “]”.";
                return false;
            }
            host = s[1..close];
            var rest = s[(close + 1)..];
            if (rest.StartsWith(':'))
                portText = rest[1..];
            else if (rest.Length > 0)
            {
                error = "Use o formato [endereço IPv6]:porta.";
                return false;
            }
        }
        else if (s.Count(ch => ch == ':') == 1)
        {
            var colon = s.IndexOf(':');
            host = s[..colon];
            portText = s[(colon + 1)..];
        }
        else
        {
            host = s; // nome, IPv4 ou IPv6 sem porta
        }

        if (host.Length == 0)
        {
            error = "Informe o nome ou IP do servidor.";
            return false;
        }
        if (host.Any(char.IsWhiteSpace) || (user?.Any(char.IsWhiteSpace) ?? false))
        {
            error = "O endereço não pode ter espaços.";
            return false;
        }

        int? port = null;
        if (portText is not null)
        {
            if (!int.TryParse(portText, out var p) || p is < 1 or > 65535)
            {
                error = "Porta inválida: use um número de 1 a 65535.";
                return false;
            }
            port = p;
        }

        result = new ConnectionAddress(host, port, user);
        return true;
    }

    /// <summary>Separa "DOMINIO\usuario" em domínio e usuário (domínio vazio se não houver "\").</summary>
    public static (string Domain, string User) SplitDomainUser(string text)
    {
        var slash = text.IndexOf('\\');
        return slash > 0 ? (text[..slash], text[(slash + 1)..]) : (string.Empty, text);
    }
}
