namespace RemoteMan.Services;

/// <summary>Monta os argumentos do ssh.exe para uma conexão.</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public static class SshArguments
{
    public static List<string> Build(
        string host, int port, string? username, string? keyFile, string? jumpHost, IEnumerable<SshTunnel>? tunnels = null)
    {
        var args = new List<string>();
        if (port is > 0 and not 22)
        {
            args.Add("-p");
            args.Add(port.ToString());
        }
        if (!string.IsNullOrWhiteSpace(keyFile))
        {
            args.Add("-i");
            args.Add(keyFile.Trim());
        }
        if (NormalizeJumpHost(jumpHost) is { Length: > 0 } jump)
        {
            args.Add("-J");
            args.Add(jump);
        }
        foreach (var tunnel in tunnels ?? [])
        {
            args.Add(tunnel.Option);
            args.Add(tunnel.Spec);
        }

        host = host.Trim();
        args.Add(string.IsNullOrWhiteSpace(username) ? host : $"{username.Trim()}@{host}");
        return args;
    }

    /// <summary>
    /// Normaliza a lista de jump hosts ("bastion, user@gw:2222" → "bastion,user@gw:2222").
    /// "none" (valor do ssh config para desativar) vira vazio.
    /// </summary>
    public static string NormalizeJumpHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return string.Join(',', value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
