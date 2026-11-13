using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RemoteMan.Models;
using RemoteMan.ViewModels;
using Windows.Storage.Pickers;

namespace RemoteMan.Views;

public sealed partial class ConnectionsPage : Page
{
    public MainViewModel ViewModel => App.ViewModel;

    public ConnectionsPage()
    {
        InitializeComponent();

        ConnectionsSource.Source = ViewModel.Groups;
        ConnectionList.ItemsSource = ConnectionsSource.View;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Selected))
                SyncListSelection();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SyncListSelection();
        ViewModel.CheckStatusIfStale();
    }

    private void SyncListSelection()
    {
        if (ReferenceEquals(ConnectionList.SelectedItem, ViewModel.Selected))
            return;

        ConnectionList.SelectedItem = ViewModel.Selected;
        if (ViewModel.Selected is not null)
            ConnectionList.ScrollIntoView(ViewModel.Selected);
    }

    private void ConnectionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsRefreshing)
            return;
        ViewModel.Selected = ConnectionList.SelectedItem as Connection;
    }

    /// <summary>Duplo clique numa conexão: conecta (mesmo comportamento do dashboard).</summary>
    private void ConnectionList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ViewModel.Selected is not null)
            ViewModel.ConnectCommand.Execute(null);
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ViewModel.SearchText = sender.Text;
    }

    private void SaveAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.SaveCommand.Execute(null);
        args.Handled = true;
    }

    private async void NewConnection_Click(object sender, RoutedEventArgs e) =>
        await App.MainWindow.NewConnectionAsync();

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Selected is not { } c)
            return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Excluir conexão",
            Content = $"Excluir \"{c.Name}\"? A senha salva também será removida.",
            PrimaryButtonText = "Excluir",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            ViewModel.DeleteSelected();
    }

    private async void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add("*");
        // app não empacotado: o picker precisa saber qual é a janela dona
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is not null && ViewModel.Selected is { } c)
            c.KeyFile = file.Path;
    }
}
