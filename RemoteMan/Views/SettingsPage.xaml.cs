using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;
using RemoteMan.Services;

namespace RemoteMan.Views;

/// <summary>Informações do ambiente (OpenSSH, Windows Terminal, arquivo de dados) e sobre o app.</summary>
public sealed partial class SettingsPage : Page
{
    private bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        _loading = true;
        BackdropBox.SelectedIndex = (int)App.Settings.Backdrop;
        CheckUpdatesSwitch.IsOn = App.Settings.CheckForUpdates;
        MonitorSwitch.IsOn = App.Settings.MonitorEnabled;
        MonitorIntervalBox.SelectedItem = MonitorIntervalBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (string)i.Tag == App.Settings.MonitorIntervalMinutes.ToString())
            ?? MonitorIntervalBox.Items[1];
        MonitorIntervalBox.IsEnabled = MonitorSwitch.IsOn;
        _loading = false;
        ShowBackdropNote(App.Settings.Backdrop, App.MainWindow.AppliedBackdrop);
    }

    private void Backdrop_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || BackdropBox.SelectedIndex < 0)
            return;

        var requested = (BackdropKind)BackdropBox.SelectedIndex;
        ShowBackdropNote(requested, App.MainWindow.ApplyBackdrop(requested));

        App.Settings.Backdrop = requested;
        SaveSettings();
    }

    private void Monitor_Toggled(object sender, RoutedEventArgs e)
    {
        MonitorIntervalBox.IsEnabled = MonitorSwitch.IsOn;
        if (_loading)
            return;
        App.Settings.MonitorEnabled = MonitorSwitch.IsOn;
        SaveSettings();
        App.ViewModel.ApplyMonitorSettings();
    }

    private void MonitorInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || MonitorIntervalBox.SelectedItem is not ComboBoxItem { Tag: string tag } || !int.TryParse(tag, out var minutes))
            return;
        App.Settings.MonitorIntervalMinutes = minutes;
        SaveSettings();
        App.ViewModel.ApplyMonitorSettings();
    }

    // a página fica em cache: detecta de novo a cada visita (o usuário pode ter instalado um cliente)
    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadSftpClients();
    }

    private void LoadSftpClients()
    {
        var installed = SftpLauncher.FindInstalled();
        var chosen = SftpArguments.Choose(App.Settings.PreferredSftpClient, installed.Keys);

        _loading = true;
        SftpClientBox.Items.Clear();
        foreach (var (kind, path) in installed)
        {
            var item = new ComboBoxItem { Content = SftpLauncher.DisplayName(kind), Tag = kind };
            ToolTipService.SetToolTip(item, path);
            SftpClientBox.Items.Add(item);
            if (kind == chosen)
                SftpClientBox.SelectedItem = item;
        }
        _loading = false;

        SftpClientRow.Visibility = installed.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var missing = Enum.GetValues<SftpClientKind>().Where(k => !installed.ContainsKey(k)).ToList();
        SftpDownloads.Content = missing.Count == 0
            ? null
            : MainWindow.SftpDownloadLinks(
                installed.Count == 0
                    ? "Nenhum cliente SFTP encontrado. Instale um dos clientes abaixo para usar “Abrir SFTP” nas conexões SSH."
                    : "Outros clientes suportados:",
                missing);
    }

    private void SftpClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || SftpClientBox.SelectedItem is not ComboBoxItem { Tag: SftpClientKind kind })
            return;
        App.Settings.PreferredSftpClient = kind;
        SaveSettings();
    }

    private void CheckUpdates_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;
        App.Settings.CheckForUpdates = CheckUpdatesSwitch.IsOn;
        SaveSettings();
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        CheckNowButton.IsEnabled = false;
        CheckResultText.Text = "Verificando…";
        CheckResultText.Visibility = Visibility.Visible;
        try
        {
            CheckResultText.Text = await App.ViewModel.CheckForUpdatesAsync();
        }
        finally
        {
            CheckNowButton.IsEnabled = true;
        }
    }

    private static void SaveSettings()
    {
        try
        {
            App.SettingsStore.Save(App.Settings);
        }
        catch (Exception ex)
        {
            App.ViewModel.ShowStatus($"Não foi possível salvar a preferência: {ex.Message}", InfoBarSeverity.Warning);
        }
    }

    /// <summary>Avisa quando o Windows não suporta o efeito escolhido e outro foi usado.</summary>
    private void ShowBackdropNote(BackdropKind requested, BackdropKind? applied)
    {
        BackdropNote.Visibility = requested == applied ? Visibility.Collapsed : Visibility.Visible;
        BackdropNote.Text = applied switch
        {
            null => "Esta versão do Windows não suporta efeitos de fundo; a janela usa uma cor sólida.",
            BackdropKind.Acrylic => "Mica não é suportado nesta versão do Windows; usando Acrílico.",
            _ => "Este efeito não é suportado nesta versão do Windows; usando Mica.",
        };
    }

    public string WindowsUser { get; } = Connection.CurrentWindowsUser;
    public string? SshPath { get; } = SshLauncher.FindSshPath();
    public bool HasWindowsTerminal { get; } = SshLauncher.FindWindowsTerminal() is not null;
    public string DataFilePath => App.ViewModel.DataFilePath;

    public ViewModels.MainViewModel ViewModel => App.ViewModel;

    // --- Credenciais compartilhadas ---

    private async void NewCredential_Click(object sender, RoutedEventArgs e) => await EditCredentialAsync(null);

    private async void EditCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SharedCredential cred })
            await EditCredentialAsync(cred);
    }

    private async Task EditCredentialAsync(SharedCredential? cred)
    {
        var dialog = new CredentialDialog(cred) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is { } result)
            App.ViewModel.SaveCredential(result, dialog.Password);
    }

    private async void DeleteCredential_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SharedCredential cred })
            return;

        var usage = App.ViewModel.CredentialUsageCount(cred);
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Excluir credencial",
            Content = usage == 0
                ? $"Excluir \"{cred.Name}\"? A senha salva também será removida."
                : $"\"{cred.Name}\" é usada por {(usage == 1 ? "1 conexão" : $"{usage} conexões")}. Ao excluir, "
                  + "elas passam a usar o mesmo usuário como próprio, mas ficam sem senha salva.",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            App.ViewModel.DeleteCredential(cred);
    }

    public string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";
}
