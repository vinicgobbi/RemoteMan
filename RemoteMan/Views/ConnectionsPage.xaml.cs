using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using RemoteMan.Models;
using RemoteMan.Services;
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
            {
                SyncListSelection();
                SyncCredentialBox();
            }
        };
        ViewModel.CredentialsChanged += (_, _) => RebuildCredentialOptions();
        RebuildCredentialOptions();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SyncListSelection();
        RebuildCredentialOptions(); // pode ter mudado em Configurações
        ViewModel.CheckStatusIfStale();
    }

    // --- Seletor de credencial (itens: "própria" + compartilhadas; Ids em lista paralela) ---

    private readonly List<Guid?> _credentialIds = [];
    private bool _syncingCredential;

    private void RebuildCredentialOptions()
    {
        _syncingCredential = true;
        _credentialIds.Clear();
        _credentialIds.Add(null);
        _credentialIds.AddRange(ViewModel.Credentials.Select(c => (Guid?)c.Id));
        CredentialBox.ItemsSource = new[] { "Usuário e senha desta conexão" }
            .Concat(ViewModel.Credentials.Select(c => c.DisplayText))
            .ToList();
        _syncingCredential = false;
        SyncCredentialBox();
    }

    private void SyncCredentialBox()
    {
        _syncingCredential = true;
        var index = _credentialIds.IndexOf(ViewModel.Selected?.CredentialId);
        CredentialBox.SelectedIndex = index < 0 ? 0 : index;
        _syncingCredential = false;
    }

    private void Credential_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingCredential || ViewModel.Selected is not { } c || CredentialBox.SelectedIndex < 0)
            return;
        c.CredentialId = _credentialIds[CredentialBox.SelectedIndex];
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

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag } && Enum.TryParse<ImportSource>(tag, out var source))
            await App.MainWindow.ImportAsync(source);
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag } && Enum.TryParse<ExportKind>(tag, out var kind))
            await App.MainWindow.ExportAsync(kind);
    }

    private async void ExportGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: ConnectionGroup group })
            // exporta o grupo inteiro, não só o que a busca atual está mostrando
            await App.MainWindow.ExportAsync(ExportKind.Backup, ViewModel.ConnectionsInGroup(group.Key), group.Key);
    }

    // acima disso, pede confirmação antes de abrir tudo
    private const int ConnectAllConfirmThreshold = 5;

    private async void ConnectGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem { Tag: ConnectionGroup group } || group.Count == 0)
            return;

        if (group.Count > ConnectAllConfirmThreshold)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Conectar a todas",
                Content = $"Abrir {group.Count} conexões do grupo \"{group.Key}\" de uma vez?",
                PrimaryButtonText = "Conectar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        ViewModel.ConnectAll(group.ToList(), group.Key);
    }

    private async void OpenSftp_Click(object sender, RoutedEventArgs e) =>
        await App.MainWindow.OpenSftpAsync(ViewModel.Selected);

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
