using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using RemoteMan.Models;
using RemoteMan.ViewModels;

namespace RemoteMan.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; } = new(App.ViewModel);

    public DashboardPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Refresh();
        ViewModel.Main.CheckStatusIfStale();
    }

    /// <summary>Clique numa linha: abre os detalhes da conexão (o botão ▶ da linha conecta).</summary>
    private void Item_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Connection c)
            App.MainWindow.ShowConnections(c);
    }

    private void NewRdp_Click(object sender, RoutedEventArgs e)
    {
        App.ViewModel.NewRdpCommand.Execute(null);
        App.MainWindow.ShowConnections();
    }

    private void NewSsh_Click(object sender, RoutedEventArgs e)
    {
        App.ViewModel.NewSshCommand.Execute(null);
        App.MainWindow.ShowConnections();
    }
}
