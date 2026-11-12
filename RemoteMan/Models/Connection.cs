using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RemoteMan.Models;

public enum ConnectionProtocol
{
    Rdp,
    Ssh,
}

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

    // --- SSH ---
    [ObservableProperty] public partial string KeyFile { get; set; }

    public static Connection Create(ConnectionProtocol protocol) => new()
    {
        Protocol = protocol,
        Name = protocol == ConnectionProtocol.Rdp ? "Nova conexão RDP" : "Nova conexão SSH",
        Port = protocol == ConnectionProtocol.Rdp ? 3389 : 22,
    };

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
        Domain = Domain,
        FullScreen = FullScreen,
        DesktopWidth = DesktopWidth,
        DesktopHeight = DesktopHeight,
        MultiMonitor = MultiMonitor,
        RedirectClipboard = RedirectClipboard,
        RedirectDrives = RedirectDrives,
        AdminSession = AdminSession,
        KeyFile = KeyFile,
    };

    /// <summary>Usuário no formato DOMINIO\usuario quando houver domínio.</summary>
    public string FullUsername =>
        string.IsNullOrWhiteSpace(Domain) ? Username : $"{Domain.Trim()}\\{Username}";

    /// <summary>Usuário logado no Windows, no formato DOMINIO\usuario.</summary>
    public static string CurrentWindowsUser =>
        System.Security.Principal.WindowsIdentity.GetCurrent().Name;
}
