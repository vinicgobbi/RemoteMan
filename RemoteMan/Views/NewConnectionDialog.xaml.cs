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
    private ConnectionAddress? _address;

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
        var defaultPort = ConnectionAddress.DefaultPort(Protocol);
        var text = AddressBox.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            SetAddress(null, $"Nome ou IP do computador. Porta padrão {defaultPort}; para outra, use servidor:porta.", error: false);
            return;
        }

        if (!ConnectionAddress.TryParse(text, out var parsed, out var error))
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

    private void SetAddress(ConnectionAddress? parsed, string hint, bool error)
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
            && (int)c.Port == (parsed.Port ?? ConnectionAddress.DefaultPort(Protocol))
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
            Port = address.Port ?? ConnectionAddress.DefaultPort(protocol),
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
            if (protocol == ConnectionProtocol.Rdp)
                (c.Domain, user) = ConnectionAddress.SplitDomainUser(user);

            c.Username = user;
            Password = PasswordInput.Password;
        }

        Result = c;
        ConnectAfter = connect;
    }
}
