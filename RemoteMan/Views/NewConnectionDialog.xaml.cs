using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using RemoteMan.Models;
using Windows.System;

namespace RemoteMan.Views;

/// <summary>
/// Diálogo curto de criação: pede só o essencial (tipo, endereço, credenciais e grupo).
/// O resultado fica em <see cref="Result"/>/<see cref="Password"/>; quem abriu decide o que fazer.
/// </summary>
public sealed partial class NewConnectionDialog : ContentDialog
{
    // lembra o último tipo escolhido durante a sessão (quem cria vários SSH seguidos não precisa trocar sempre)
    private static ConnectionProtocol s_lastProtocol = ConnectionProtocol.Rdp;

    private readonly IReadOnlyList<Connection> _existing;
    private ParsedAddress? _address;

    public NewConnectionDialog(IEnumerable<string> groups, string? defaultGroup, IReadOnlyList<Connection> existing)
    {
        InitializeComponent();
        _existing = existing;

        GroupBox.ItemsSource = groups.ToList();
        GroupBox.Text = defaultGroup?.Trim() ?? string.Empty;
        ProtocolSelector.SelectedIndex = s_lastProtocol == ConnectionProtocol.Ssh ? 1 : 0;
        UpdateProtocolUi();
        UpdateAddress();
    }

    /// <summary>Conexão montada a partir do formulário (null se o usuário cancelou).</summary>
    public Connection? Result { get; private set; }

    /// <summary>Senha digitada (vazia = não salvar senha).</summary>
    public string Password { get; private set; } = string.Empty;

    /// <summary>True quando o usuário escolheu "Criar e conectar".</summary>
    public bool ConnectAfter { get; private set; }

    private ConnectionProtocol Protocol =>
        ProtocolSelector.SelectedIndex == 1 ? ConnectionProtocol.Ssh : ConnectionProtocol.Rdp;

    private bool UsesWindowsLogin => Protocol == ConnectionProtocol.Rdp && WindowsLoginBox.IsChecked == true;

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        AddressBox.Focus(FocusState.Programmatic);

    // --- Eventos do formulário ---

    private void Protocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateProtocolUi();
        UpdateAddress(); // a porta padrão e a checagem de duplicadas dependem do tipo
    }

    private void Address_TextChanged(object sender, TextChangedEventArgs e) => UpdateAddress();

    private void WindowsLogin_Changed(object sender, RoutedEventArgs e) => UpdateProtocolUi();

    /// <summary>Enter em qualquer campo cria a conexão (mesmo efeito do botão "Criar").</summary>
    private void Content_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _address is null)
            return;
        if (e.OriginalSource is ComboBox { IsDropDownOpen: true })
            return; // Enter confirma o item da lista de grupos

        e.Handled = true;
        Accept(connect: false);
        Hide();
    }

    private void Primary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args) => Accept(connect: true);

    private void Secondary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args) => Accept(connect: false);

    // --- Estado da tela ---

    private void UpdateProtocolUi()
    {
        var rdp = Protocol == ConnectionProtocol.Rdp;

        WindowsLoginBox.Visibility = rdp ? Visibility.Visible : Visibility.Collapsed;
        CredentialsPanel.Visibility = UsesWindowsLogin ? Visibility.Collapsed : Visibility.Visible;
        UserBox.PlaceholderText = rdp ? @"usuario ou DOMINIO\usuario" : "root";

        FooterHint.Text = rdp
            ? "A senha fica guardada no Gerenciador de Credenciais do Windows. Resolução, monitores e "
              + "outras opções podem ser ajustadas depois em “Editar”."
            : "A senha fica guardada no Gerenciador de Credenciais do Windows. Para usar uma chave "
              + "privada, ajuste depois em “Editar”.";
    }

    private void UpdateAddress()
    {
        var defaultPort = DefaultPort(Protocol);
        var text = AddressBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            SetAddress(null, $"Nome ou IP do computador. Porta padrão {defaultPort}; para outra, use servidor:porta.", error: false);
            return;
        }

        if (!ParsedAddress.TryParse(text, out var parsed, out var error))
        {
            SetAddress(null, error, error: true);
            return;
        }

        var port = parsed.Port ?? defaultPort;
        var hint = parsed.Port is null
            ? $"Porta {port} (padrão do {(Protocol == ConnectionProtocol.Rdp ? "RDP" : "SSH")}). Para outra, use servidor:porta."
            : $"Porta {port}.";
        if (parsed.User is not null)
            hint += $" Usuário: {parsed.User}.";

        SetAddress(parsed, hint, error: false);
    }

    private void SetAddress(ParsedAddress? parsed, string hint, bool error)
    {
        _address = parsed;
        AddressHint.Text = hint;
        AddressHint.Foreground = (Brush)Application.Current.Resources[
            error ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush"];

        NameBox.PlaceholderText = parsed?.Host ?? "Igual ao endereço";
        IsPrimaryButtonEnabled = IsSecondaryButtonEnabled = parsed is not null;

        // aviso (não bloqueia): já existe uma conexão igual
        var duplicate = parsed is null ? null : _existing.FirstOrDefault(c =>
            c.Protocol == Protocol
            && (int)c.Port == (parsed.Port ?? DefaultPort(Protocol))
            && string.Equals(c.Host.Trim(), parsed.Host, StringComparison.OrdinalIgnoreCase));

        DuplicateInfo.IsOpen = duplicate is not null;
        if (duplicate is not null)
            DuplicateInfo.Message = $"Já existe uma conexão para este endereço: \"{duplicate.Name}\".";
    }

    // --- Resultado ---

    private void Accept(bool connect)
    {
        if (_address is not { } address)
            return;

        var protocol = Protocol;
        s_lastProtocol = protocol;

        var name = NameBox.Text.Trim();
        var c = new Connection
        {
            Protocol = protocol,
            Host = address.Host,
            Port = address.Port ?? DefaultPort(protocol),
            Name = name.Length > 0 ? name : address.Host,
            Group = GroupBox.Text?.Trim() ?? string.Empty,
            UseWindowsCredentials = UsesWindowsLogin,
        };

        if (!c.UseWindowsCredentials)
        {
            var user = UserBox.Text.Trim();
            if (user.Length == 0 && address.User is not null)
                user = address.User;

            // RDP: "DOMINIO\usuario" vai para os campos Domínio e Usuário
            var slash = user.IndexOf('\\');
            if (protocol == ConnectionProtocol.Rdp && slash > 0)
            {
                c.Domain = user[..slash];
                user = user[(slash + 1)..];
            }

            c.Username = user;
            Password = PasswordInput.Password;
        }

        Result = c;
        ConnectAfter = connect;
    }

    private static int DefaultPort(ConnectionProtocol p) => p == ConnectionProtocol.Rdp ? 3389 : 22;

    /// <summary>
    /// Endereço digitado pelo usuário. Aceita "host", "host:porta", "[ipv6]:porta",
    /// "usuario@host" e prefixos como "ssh://" ou "rdp://".
    /// </summary>
    private sealed record ParsedAddress(string Host, int? Port, string? User)
    {
        public static bool TryParse(string text, out ParsedAddress result, out string error)
        {
            result = null!;
            error = string.Empty;

            var s = text.Trim();
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

            result = new ParsedAddress(host, port, user);
            return true;
        }
    }
}
