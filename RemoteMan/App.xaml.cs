using Microsoft.UI.Xaml;
using RemoteMan.Models;
using RemoteMan.Services;
using RemoteMan.ViewModels;

namespace RemoteMan;

public partial class App : Application
{
    /// <summary>Estado compartilhado entre as páginas (lista de conexões, seleção, status).</summary>
    public static MainViewModel ViewModel { get; private set; } = null!;

    public static MainWindow MainWindow { get; private set; } = null!;

    public static SettingsStore SettingsStore { get; } = new();

    /// <summary>Preferências do app (efeito de fundo etc.).</summary>
    public static AppSettings Settings { get; private set; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Settings = SettingsStore.Load();
        ViewModel = new MainViewModel();
        MainWindow = new MainWindow();
        MainWindow.Activate();

        // em segundo plano: não atrasa a abertura e falha em silêncio
        _ = ViewModel.CheckForUpdatesIfDueAsync();
    }
}
