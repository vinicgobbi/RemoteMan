using Microsoft.UI.Xaml;
using RemoteMan.Models;

namespace RemoteMan.Helpers;

/// <summary>Funções usadas nos x:Bind da interface (substituem converters).</summary>
public static class Ui
{
    public static string ProtocolGlyph(ConnectionProtocol p) =>
        p == ConnectionProtocol.Rdp ? "" : ""; // monitor / prompt de comando

    public static string ProtocolName(ConnectionProtocol p) =>
        p == ConnectionProtocol.Rdp ? "RDP" : "SSH";

    public static string Endpoint(string host, double port) =>
        string.IsNullOrWhiteSpace(host) ? "(sem host)" : $"{host}:{(int)port}";

    public static string Summary(ConnectionProtocol p, string host, double port) =>
        $"{ProtocolName(p)} · {Endpoint(host, port)}";

    public static Visibility VisibleIfNotNull(object? o) => o is null ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility CollapsedIfNotNull(object? o) => o is null ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisibleIf(bool b) => b ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility RdpOnly(ConnectionProtocol p) => VisibleIf(p == ConnectionProtocol.Rdp);
    public static Visibility SshOnly(ConnectionProtocol p) => VisibleIf(p == ConnectionProtocol.Ssh);

    public static bool Not(bool b) => !b;

    /// <summary>Campos de usuário/senha: escondidos quando o RDP usa o usuário do Windows.</summary>
    public static Visibility ManualCredentials(ConnectionProtocol p, bool useWindows) =>
        VisibleIf(!(p == ConnectionProtocol.Rdp && useWindows));

    public static Visibility WindowsCredentials(ConnectionProtocol p, bool useWindows) =>
        VisibleIf(p == ConnectionProtocol.Rdp && useWindows);

    public static string WindowsUserInfo() =>
        $"Conectará como {Connection.CurrentWindowsUser}. Sem a política de delegação de credenciais " +
        "padrão (SSO), o mstsc pedirá a senha com esse usuário já preenchido.";

    public static string PasswordPlaceholder(bool hasSaved) =>
        hasSaved ? "•••••••• salva (digite para substituir)" : "Nenhuma senha salva";
}
