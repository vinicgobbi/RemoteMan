using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using RemoteMan.Models;

namespace RemoteMan.Helpers;

/// <summary>Funções usadas nos x:Bind da interface (substituem converters).</summary>
public static class Ui
{
    // --- Protocolo e endereço ---
    public static string ProtocolGlyph(ConnectionProtocol p) =>
        p == ConnectionProtocol.Rdp ? "" : ""; // monitor / prompt de comando

    public static string ProtocolName(ConnectionProtocol p) =>
        p == ConnectionProtocol.Rdp ? "RDP" : "SSH";

    public static string ProtocolLongName(ConnectionProtocol p) =>
        p == ConnectionProtocol.Rdp ? "Área de trabalho remota (RDP)" : "Terminal SSH";

    public static string Endpoint(string host, double port) =>
        string.IsNullOrWhiteSpace(host) ? "(sem host)" : $"{host}:{(int)port}";

    public static string Summary(ConnectionProtocol p, string host, double port) =>
        $"{ProtocolName(p)} · {Endpoint(host, port)}";

    // --- Visibilidade ---
    public static Visibility VisibleIfNotNull(object? o) => o is null ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility CollapsedIfNotNull(object? o) => o is null ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility VisibleIf(bool b) => b ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility CollapsedIf(bool b) => b ? Visibility.Collapsed : Visibility.Visible;
    public static Visibility VisibleIfText(string? s) => VisibleIf(!string.IsNullOrWhiteSpace(s));

    public static Visibility RdpOnly(ConnectionProtocol p) => VisibleIf(p == ConnectionProtocol.Rdp);
    public static Visibility SshOnly(ConnectionProtocol p) => VisibleIf(p == ConnectionProtocol.Ssh);

    public static bool Not(bool b) => !b;

    public static Visibility VisibleIfAny(int count) => VisibleIf(count > 0);
    public static Visibility CollapsedIfAny(int count) => VisibleIf(count == 0);

    // --- Favoritos ---
    public static string FavoriteGlyph(bool isFavorite) => isFavorite ? "" : ""; // estrela cheia / vazia
    public static string FavoriteTooltip(bool isFavorite) => isFavorite ? "Remover dos favoritos" : "Adicionar aos favoritos";

    // --- Status online/offline ---
    public static Brush StatusBrush(ReachabilityStatus s) => (Brush)Application.Current.Resources[s switch
    {
        ReachabilityStatus.Online => "SystemFillColorSuccessBrush",
        ReachabilityStatus.Offline => "SystemFillColorCriticalBrush",
        ReachabilityStatus.Checking => "SystemFillColorCautionBrush",
        _ => "ControlStrongFillColorDisabledBrush",
    }];

    public static string StatusText(ReachabilityStatus s) => s switch
    {
        ReachabilityStatus.Online => "Online",
        ReachabilityStatus.Offline => "Offline",
        ReachabilityStatus.Checking => "Verificando…",
        _ => "Não verificado",
    };

    // --- Datas e contagens ---
    public static string RelativeTime(DateTime? when)
    {
        if (when is not { } t)
            return "Nunca";

        var diff = DateTime.Now - t;
        if (diff.TotalMinutes < 1) return "agora mesmo";
        if (diff.TotalMinutes < 60) return $"há {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"há {(int)diff.TotalHours} h";
        if (diff.TotalDays < 2) return "ontem";
        if (diff.TotalDays < 30) return $"há {(int)diff.TotalDays} dias";
        return t.ToString("dd/MM/yyyy");
    }

    public static string LastCheckText(DateTime? when, bool checking) =>
        checking ? "Testando a porta de cada servidor…" : when is null ? "Ainda não verificado" : $"Verificado {RelativeTime(when)}";

    public static string UsageText(int count, DateTime? last) => count switch
    {
        0 => "Nunca usada",
        1 => $"1 vez · última {RelativeTime(last)}",
        _ => $"{count} vezes · última {RelativeTime(last)}",
    };

    // usados pelas telas anteriores ao redesenho
    public static string UsageText(int count) => count == 1 ? "1 vez" : $"{count} vezes";
    public static string GroupsText(int count) => count == 1 ? "em 1 grupo" : $"em {count} grupos";
    public static string Number(int n) => n.ToString();
    public static string ConnectionDetail(ConnectionProtocol p, DateTime? when) =>
        $"{ProtocolName(p)} · {RelativeTime(when)}";

    // --- Valores exibidos no modo leitura ---
    public static Brush LabelBrush(string? value) => (Brush)Application.Current.Resources[
        string.IsNullOrEmpty(value) ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"];


    public static string OrDash(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
    public static string GroupText(string? group) => string.IsNullOrWhiteSpace(group) ? "Sem grupo" : group;
    public static string YesNo(bool b) => b ? "Sim" : "Não";
    public static string PortText(double port) => ((int)port).ToString();

    public static string ResolutionText(bool fullScreen, double width, double height) =>
        fullScreen ? "Tela cheia" : $"Janela de {(int)width} × {(int)height}";

    public static string UserText(ConnectionProtocol p, bool useWindows, string user, string domain)
    {
        if (p == ConnectionProtocol.Rdp && useWindows)
            return $"{Connection.CurrentWindowsUser} (usuário do Windows)";
        if (string.IsNullOrWhiteSpace(user))
            return "Não informado";
        return p == ConnectionProtocol.Rdp && !string.IsNullOrWhiteSpace(domain) ? $"{domain.Trim()}\\{user}" : user;
    }

    public static string PasswordText(bool hasSaved) => hasSaved ? "Salva no Windows" : "Nenhuma senha salva";

    // --- Ambiente ---
    public static string OrNotFound(string? path) => path ?? "Não encontrado";
    public static string FoundText(bool found) => found ? "Instalado" : "Não encontrado (será usada uma janela de console)";

    // --- Autenticação no modo edição ---
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
