using Microsoft.UI.Xaml;
using RemoteMan.ViewModels;

namespace RemoteMan;

public partial class App : Application
{
    /// <summary>Estado compartilhado entre as páginas (lista de conexões, seleção, status).</summary>
    public static MainViewModel ViewModel { get; private set; } = null!;

    public static MainWindow MainWindow { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        ViewModel = new MainViewModel();
        MainWindow = new MainWindow();
        MainWindow.Activate();
    }
}
