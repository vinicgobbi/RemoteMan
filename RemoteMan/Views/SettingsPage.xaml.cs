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

    public string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";
}
