using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RemoteMan.Models;
using RemoteMan.Services;
using Windows.System;

namespace RemoteMan.Views;

/// <summary>Conexão rápida (Ctrl+K): busca por nome, host, grupo ou usuário e conecta com Enter.</summary>
public sealed partial class QuickConnectDialog : ContentDialog
{
    private readonly IReadOnlyList<Connection> _connections;

    public QuickConnectDialog(IReadOnlyList<Connection> connections)
    {
        InitializeComponent();
        _connections = connections;
        UpdateResults();
    }

    /// <summary>Conexão escolhida (null se o usuário fechou sem escolher).</summary>
    public Connection? Result { get; private set; }

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args) =>
        QueryBox.Focus(FocusState.Programmatic);

    private void Query_TextChanged(object sender, TextChangedEventArgs e) => UpdateResults();

    /// <summary>Setas movem a seleção sem tirar o foco da caixa de texto; Enter conecta.</summary>
    private void Query_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var count = ResultsList.Items.Count;
        switch (e.Key)
        {
            case VirtualKey.Down when count > 0:
                ResultsList.SelectedIndex = (ResultsList.SelectedIndex + 1) % count;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                e.Handled = true;
                break;

            case VirtualKey.Up when count > 0:
                ResultsList.SelectedIndex = (ResultsList.SelectedIndex - 1 + count) % count;
                ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                e.Handled = true;
                break;

            case VirtualKey.Enter:
                e.Handled = true;
                if (ResultsList.SelectedItem is Connection c)
                    Choose(c);
                break;
        }
    }

    private void Results_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Connection c)
            Choose(c);
    }

    private void Choose(Connection c)
    {
        Result = c;
        Hide();
    }

    private void UpdateResults()
    {
        var query = QueryBox.Text;
        var results = QuickSearch.Search(
            _connections,
            c => new QuickSearchEntry(c.Name, c.Host, c.Group, c.Username, c.IsFavorite, c.LastConnected),
            query);

        ResultsList.ItemsSource = results;
        ResultsList.SelectedIndex = results.Count > 0 ? 0 : -1;

        EmptyText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Text = _connections.Count == 0
            ? "Nenhuma conexão cadastrada ainda."
            : $"Nenhuma conexão encontrada para “{query.Trim()}”.";
    }
}
