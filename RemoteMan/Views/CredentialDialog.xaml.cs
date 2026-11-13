using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;

namespace RemoteMan.Views;

/// <summary>Cria ou edita uma credencial compartilhada.</summary>
public sealed partial class CredentialDialog : ContentDialog
{
    private readonly SharedCredential? _editing;

    /// <param name="editing">Credencial a editar (null = nova).</param>
    public CredentialDialog(SharedCredential? editing)
    {
        InitializeComponent();
        _editing = editing;

        Title = editing is null ? "Nova credencial" : "Editar credencial";
        if (editing is not null)
        {
            NameBox.Text = editing.Name;
            UserBox.Text = editing.FullUsername;
            PasswordInput.PlaceholderText = "Deixe em branco para manter a senha atual";
        }
    }

    /// <summary>Credencial preenchida (null se cancelado). Na edição, é a mesma instância atualizada.</summary>
    public SharedCredential? Result { get; private set; }

    /// <summary>Senha digitada (null = manter a atual).</summary>
    public string? Password { get; private set; }

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        NameBox.Focus(FocusState.Programmatic);

    private void Name_TextChanged(object sender, TextChangedEventArgs e) =>
        IsPrimaryButtonEnabled = NameBox.Text.Trim().Length > 0;

    private void Primary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var (domain, user) = ConnectionAddress.SplitDomainUser(UserBox.Text.Trim());
        var cred = _editing ?? new SharedCredential();
        cred.Name = NameBox.Text.Trim();
        cred.Username = user;
        cred.Domain = domain;

        Result = cred;
        Password = string.IsNullOrEmpty(PasswordInput.Password) ? null : PasswordInput.Password;
    }
}
