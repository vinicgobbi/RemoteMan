using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using RemoteMan.Models;
using RemoteMan.ViewModels;
using RemoteMan.Views;
using Windows.Graphics;

namespace RemoteMan;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel => App.ViewModel;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        SystemBackdrop = new MicaBackdrop();
        AppWindow.Resize(new SizeInt32(1280, 820));

        Closed += (_, _) => ViewModel.SaveAll();

        // tela inicial: dashboard
        NavView.SelectedItem = HomeNavItem;
    }

    /// <summary>Vai para o dashboard.</summary>
    public void ShowDashboard() => NavView.SelectedItem = HomeNavItem;

    /// <summary>Vai para a tela de conexões, opcionalmente já com uma conexão selecionada.</summary>
    public void ShowConnections(Connection? select = null)
    {
        if (select is not null)
            ViewModel.Reveal(select);
        NavView.SelectedItem = ConnectionsNavItem;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        var page = tag switch
        {
            "connections" => typeof(ConnectionsPage),
            _ => typeof(DashboardPage),
        };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
    }
}
