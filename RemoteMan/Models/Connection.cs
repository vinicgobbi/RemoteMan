using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RemoteMan.Models;

/// <summary>Resultado do teste de porta TCP (só em memória, não é salvo).</summary>
public enum ReachabilityStatus
{
    Unknown,
    Checking,
    Online,
    Offline,
}

/// <summary>
/// Uma conexão salva. Senhas NÃO ficam aqui — vão para o Windows Credential Manager
/// (ver <see cref="Services.CredentialStore"/>).
/// </summary>
public partial class Connection : ObservableObject
{
    public Connection()
    {
        Name = string.Empty;
        Group = string.Empty;
        Host = string.Empty;
        Username = string.Empty;
        Domain = string.Empty;
        KeyFile = string.Empty;
        JumpHost = string.Empty;
        Tunnels = string.Empty;
        GatewayHost = string.Empty;
        GatewayUseSameCredentials = true;
        Notes = string.Empty;
        Port = 3389;
        FullScreen = true;
        DesktopWidth = 1920;
        DesktopHeight = 1080;
        RedirectClipboard = true;
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    [ObservableProperty] public partial ConnectionProtocol Protocol { get; set; }
    [ObservableProperty] public partial string Name { get; set; }
    [ObservableProperty] public partial string Group { get; set; }
    [ObservableProperty] public partial string Host { get; set; }

    // double porque NumberBox.Value é double (permite x:Bind TwoWay direto)
    [ObservableProperty] public partial double Port { get; set; }
    [ObservableProperty] public partial string Username { get; set; }
    [ObservableProperty] public partial string Notes { get; set; }
    [ObservableProperty] public partial DateTime? LastConnected { get; set; }
    [ObservableProperty] public partial int ConnectCount { get; set; }
    [ObservableProperty] public partial bool IsFavorite { get; set; }

    [JsonIgnore]
    [ObservableProperty] public partial ReachabilityStatus Status { get; set; }

    /// <summary>Credencial compartilhada usada por esta conexão (null = usuário e senha próprios).</summary>
    [ObservableProperty] public partial Guid? CredentialId { get; set; }

    // --- RDP ---
    /// <summary>Conecta com o usuário logado no Windows em vez de usuário/senha salvos.</summary>
    [ObservableProperty] public partial bool UseWindowsCredentials { get; set; }
    [ObservableProperty] public partial string Domain { get; set; }
    [ObservableProperty] public partial bool FullScreen { get; set; }
    [ObservableProperty] public partial double DesktopWidth { get; set; }
    [ObservableProperty] public partial double DesktopHeight { get; set; }
    [ObservableProperty] public partial bool MultiMonitor { get; set; }
    [ObservableProperty] public partial bool RedirectClipboard { get; set; }
    [ObservableProperty] public partial bool RedirectDrives { get; set; }
    [ObservableProperty] public partial bool AdminSession { get; set; }

    /// <summary>Servidor RD Gateway (vazio = conexão direta).</summary>
    [ObservableProperty] public partial string GatewayHost { get; set; }

    /// <summary>Não usa o gateway para endereços da rede local.</summary>
    [ObservableProperty] public partial bool GatewayBypassLocal { get; set; }

    /// <summary>Mesmas credenciais no gateway e no computador remoto.</summary>
    [ObservableProperty] public partial bool GatewayUseSameCredentials { get; set; }

    // --- SSH ---
    [ObservableProperty] public partial string KeyFile { get; set; }

    /// <summary>Servidor(es) intermediário(s) para o ssh -J (ex.: "bastion" ou "user@gw:2222,outro").</summary>
    [ObservableProperty] public partial string JumpHost { get; set; }

    /// <summary>Túneis SSH, um por linha (ex.: "L 8080:localhost:80", "D 1080"). Ver Services.SshTunnels.</summary>
    [ObservableProperty] public partial string Tunnels { get; set; }

    /// <summary>Cópia com novo Id (não usa MemberwiseClone para não copiar os handlers de PropertyChanged).</summary>
    public Connection Clone() => new()
    {
        Protocol = Protocol,
        Name = Name,
        Group = Group,
        Host = Host,
        Port = Port,
        Username = Username,
        Notes = Notes,
        UseWindowsCredentials = UseWindowsCredentials,
        CredentialId = CredentialId,
        Domain = Domain,
        FullScreen = FullScreen,
        DesktopWidth = DesktopWidth,
        DesktopHeight = DesktopHeight,
        MultiMonitor = MultiMonitor,
        RedirectClipboard = RedirectClipboard,
        RedirectDrives = RedirectDrives,
        AdminSession = AdminSession,
        KeyFile = KeyFile,
        JumpHost = JumpHost,
        Tunnels = Tunnels,
        GatewayHost = GatewayHost,
        GatewayBypassLocal = GatewayBypassLocal,
        GatewayUseSameCredentials = GatewayUseSameCredentials,
    };

    /// <summary>Usuário no formato DOMINIO\usuario quando houver domínio.</summary>
    public string FullUsername =>
        string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain.Trim()}\\{Username}";

    /// <summary>Usuário logado no Windows, no formato DOMINIO\usuario.</summary>
    public static string CurrentWindowsUser =>
        System.Security.Principal.WindowsIdentity.GetCurrent().Name;
}
