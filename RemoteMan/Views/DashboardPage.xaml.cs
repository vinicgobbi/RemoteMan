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
        ViewModel.CheckStatusIfStale();
    }

    private void Connect(Connection? c)
    {
        App.ViewModel.ConnectTo(c);
        ViewModel.Refresh();
    }

    private void ConnectItem_Click(object sender, RoutedEventArgs e) =>
        Connect((sender as FrameworkElement)?.Tag as Connection);

    private void ReconnectLast_Click(object sender, RoutedEventArgs e) =>
        Connect(ViewModel.LastConnection);

    private void EditLast_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LastConnection is { } c)
            App.MainWindow.ShowConnections(c);
    }

    /// <summary>Clique numa linha de qualquer lista: abre a conexão para edição.</summary>
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
