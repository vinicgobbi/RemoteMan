namespace RemoteMan.Services;

public enum TunnelKind
{
    /// <summary>-L: porta local encaminhada para um destino visto pelo servidor.</summary>
    Local,

    /// <summary>-R: porta no servidor encaminhada para um destino visto pela máquina local.</summary>
    Remote,

    /// <summary>-D: proxy SOCKS local.</summary>
    Dynamic,
}

/// <summary>Um túnel SSH. Host/HostPort ficam nulos no tipo Dynamic.</summary>
public sealed record SshTunnel(TunnelKind Kind, string? BindAddress, int Port, string? Host, int? HostPort)
{
    private string Bind => BindAddress is { Length: > 0 } b ? $"{Bracket(b)}:" : string.Empty;

    /// <summary>Argumento do ssh (sem a opção): "[bind:]porta:host:portaHost" ou "[bind:]porta".</summary>
    public string Spec => Kind == TunnelKind.Dynamic
        ? $"{Bind}{Port}"
        : $"{Bind}{Port}:{Bracket(Host!)}:{HostPort}";

    public string Option => Kind switch
    {
        TunnelKind.Local => "-L",
        TunnelKind.Remote => "-R",
        _ => "-D",
    };

    /// <summary>Linha no formato do editor (ex.: "L 8080:localhost:80").</summary>
    public string Line => $"{Option[1]} {Spec}";

    /// <summary>A porta fica aberta nesta máquina (Local e Dynamic).</summary>
    public bool ListensLocally => Kind != TunnelKind.Remote;

    private static string Bracket(string host) => host.Contains(':') ? $"[{host}]" : host;
}

/// <summary>Leitura e escrita da lista de túneis (uma por linha).</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public static class SshTunnels
{
    /// <summary>
    /// Lê linhas como "L 8080:localhost:80", "R 9000:localhost:3000", "D 1080" (aceita "-L" e bind opcional).
    /// Linhas vazias e comentários (#) são ignorados.
    /// </summary>
    public static (List<SshTunnel> Tunnels, List<string> Errors) Parse(string? text)
    {
        var tunnels = new List<SshTunnel>();
        var errors = new List<string>();
        var lineNumber = 0;

        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var space = line.IndexOfAny([' ', '\t']);
            var kindText = (space < 0 ? line : line[..space]).TrimStart('-');
            var spec = space < 0 ? string.Empty : line[space..].Trim();

            TunnelKind? kind = kindText.ToUpperInvariant() switch
            {
                "L" => TunnelKind.Local,
                "R" => TunnelKind.Remote,
                "D" => TunnelKind.Dynamic,
                _ => null,
            };

            if (kind is null)
                errors.Add($"Linha {lineNumber}: use L (local), R (remoto) ou D (SOCKS) no início.");
            else if (TryParseSpec(kind.Value, spec, out var tunnel, out var error))
                tunnels.Add(tunnel);
            else
                errors.Add($"Linha {lineNumber}: {error}");
        }

        return (tunnels, errors);
    }

    /// <summary>"[bind:]porta:host:portaHost" (Local/Remote) ou "[bind:]porta" (Dynamic).</summary>
    public static bool TryParseSpec(TunnelKind kind, string spec, out SshTunnel tunnel, out string error)
    {
        tunnel = null!;
        error = string.Empty;
        var parts = SplitOutsideBrackets(spec.Trim());

        string? bind = null;
        if (kind == TunnelKind.Dynamic)
        {
            if (parts.Count is < 1 or > 2 || parts[^1].Length == 0)
            {
                error = "use D porta (ex.: D 1080).";
                return false;
            }
            if (parts.Count == 2)
                bind = parts[0];
            if (!TryPort(parts[^1], out var port))
            {
                error = $"porta inválida ({parts[^1]}).";
                return false;
            }
            tunnel = new SshTunnel(kind, bind, port, null, null);
            return true;
        }

        if (parts.Count is < 3 or > 4)
        {
            error = $"use {(kind == TunnelKind.Local ? "L" : "R")} porta:host:porta (ex.: L 8080:localhost:80).";
            return false;
        }
        if (parts.Count == 4)
        {
            bind = parts[0];
            parts.RemoveAt(0);
        }

        if (!TryPort(parts[0], out var listen))
        {
            error = $"porta inválida ({parts[0]}).";
            return false;
        }
        if (parts[1].Length == 0)
        {
            error = "informe o host de destino.";
            return false;
        }
        if (!TryPort(parts[2], out var hostPort))
        {
            error = $"porta de destino inválida ({parts[2]}).";
            return false;
        }

        tunnel = new SshTunnel(kind, bind, listen, parts[1], hostPort);
        return true;
    }

    /// <summary>
    /// Converte uma opção do ssh config em túnel: LocalForward/RemoteForward "[bind:]porta host:portaHost",
    /// DynamicForward "[bind:]porta".
    /// </summary>
    public static bool TryParseConfigForward(TunnelKind kind, string value, out SshTunnel tunnel)
    {
        var tokens = value.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var spec = kind == TunnelKind.Dynamic || tokens.Length != 2 ? string.Join(' ', tokens) : $"{tokens[0]}:{tokens[1]}";
        return TryParseSpec(kind, spec, out tunnel, out _);
    }

    /// <summary>Opção do ssh config equivalente (ex.: "LocalForward 8080 localhost:80").</summary>
    public static string ToConfigLine(SshTunnel t) => t.Kind switch
    {
        TunnelKind.Dynamic => $"DynamicForward {t.Spec}",
        _ => $"{(t.Kind == TunnelKind.Local ? "LocalForward" : "RemoteForward")} " +
             $"{(t.BindAddress is { Length: > 0 } b ? $"{(b.Contains(':') ? $"[{b}]" : b)}:" : "")}{t.Port} " +
             $"{(t.Host!.Contains(':') ? $"[{t.Host}]" : t.Host)}:{t.HostPort}",
    };

    public static string ToText(IEnumerable<SshTunnel> tunnels) => string.Join('\n', tunnels.Select(t => t.Line));

    private static bool TryPort(string text, out int port) =>
        int.TryParse(text, out port) && port is >= 1 and <= 65535;

    /// <summary>Divide por ":" fora de colchetes (para IPv6: "[::1]:80" → "::1", "80").</summary>
    private static List<string> SplitOutsideBrackets(string s)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        foreach (var ch in s)
        {
            if (ch == '[') { depth++; continue; }
            if (ch == ']') { depth = Math.Max(0, depth - 1); continue; }
            if (ch == ':' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(ch);
        }
        parts.Add(current.ToString().Trim());
        return parts;
    }
}
