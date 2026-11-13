using System.Collections.ObjectModel;
using RemoteMan.Models;

namespace RemoteMan.ViewModels;

/// <summary>Grupo exibido na lista (cabeçalho = Key).</summary>
public sealed class ConnectionGroup(string key, IEnumerable<Connection> items)
    : ObservableCollection<Connection>(items)
{
    public string Key { get; } = key;

    public string ConnectAllText => $"Conectar a todas ({Count})";
}
