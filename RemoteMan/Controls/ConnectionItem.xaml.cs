using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using RemoteMan.Models;

namespace RemoteMan.Controls;

/// <summary>Linha padrão de uma conexão nas listas (lista de conexões e dashboard).</summary>
public sealed partial class ConnectionItem : UserControl
{
    public static readonly DependencyProperty ConnectionProperty = DependencyProperty.Register(
        nameof(Connection), typeof(Connection), typeof(ConnectionItem), new PropertyMetadata(null));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(ConnectionItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ShowFavoriteProperty = DependencyProperty.Register(
        nameof(ShowFavorite), typeof(bool), typeof(ConnectionItem), new PropertyMetadata(false));

    public ConnectionItem()
    {
        InitializeComponent();
    }

    public Connection? Connection
    {
        get => (Connection?)GetValue(ConnectionProperty);
        set => SetValue(ConnectionProperty, value);
    }

    /// <summary>Texto curto à direita (ex.: "há 5 min").</summary>
    public string Detail
    {
        get => (string)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    /// <summary>Mostra a estrela quando a conexão é favorita.</summary>
    public bool ShowFavorite
    {
        get => (bool)GetValue(ShowFavoriteProperty);
        set => SetValue(ShowFavoriteProperty, value);
    }

    private void Connect_Click(object sender, RoutedEventArgs e) => App.ViewModel.ConnectTo(Connection);

    // impede que o duplo clique no botão chegue à lista (que também conectaria)
    private void Connect_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => e.Handled = true;
}
