namespace RemoteMan.Models;

/// <summary>
/// Conexão lida de uma fonte externa (.rdp, ~/.ssh/config, PuTTY), antes de virar uma <see cref="Connection"/>.
/// Opções nulas mantêm o padrão do app.
/// </summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public sealed record ImportedConnection
{
    public required ConnectionProtocol Protocol { get; init; }
    public required string Name { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;

    /// <summary>Chave privada (SSH).</summary>
    public string KeyFile { get; init; } = string.Empty;

    /// <summary>Jump host para o ssh -J (SSH).</summary>
    public string JumpHost { get; init; } = string.Empty;

    // --- RDP ---
    public bool? FullScreen { get; init; }
    public int? DesktopWidth { get; init; }
    public int? DesktopHeight { get; init; }
    public bool? MultiMonitor { get; init; }
    public bool? RedirectClipboard { get; init; }
    public bool? RedirectDrives { get; init; }
    public bool? AdminSession { get; init; }
    public string GatewayHost { get; init; } = string.Empty;
    public bool? GatewayBypassLocal { get; init; }
    public bool? GatewayUseSameCredentials { get; init; }

    /// <summary>Algo que não pôde ser importado (ex.: chave .ppk, RD Gateway).</summary>
    public string? Warning { get; init; }
}

/// <summary>Resultado de uma leitura: conexões encontradas e problemas que impediram itens de entrar.</summary>
public sealed record ImportResult(IReadOnlyList<ImportedConnection> Connections, IReadOnlyList<string> Errors)
{
    public static ImportResult Empty { get; } = new([], []);
}
