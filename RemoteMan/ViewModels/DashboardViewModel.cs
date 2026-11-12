using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using RemoteMan.Models;

namespace RemoteMan.ViewModels;

/// <summary>Dados da tela inicial, calculados a partir das conexões do <see cref="MainViewModel"/>.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private const int MaxRecent = 8;

    public DashboardViewModel(MainViewModel main)
    {
        Main = main;
        Summary = string.Empty;
        main.DataChanged += (_, _) => Refresh();
    }

    /// <summary>Estado compartilhado (status online/offline, comandos).</summary>
    public MainViewModel Main { get; }

    public ObservableCollection<Connection> Recent { get; } = [];
    public ObservableCollection<Connection> Favorites { get; } = [];

    [ObservableProperty] public partial int TotalCount { get; set; }

    /// <summary>Ex.: "14 conexões em 3 grupos · 9 RDP · 5 SSH".</summary>
    [ObservableProperty] public partial string Summary { get; set; }

    public string Greeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var period = hour < 12 ? "Bom dia" : hour < 18 ? "Boa tarde" : "Boa noite";
            var user = Connection.CurrentWindowsUser.Split('\\').Last();
            return $"{period}, {user}";
        }
    }

    /// <summary>Recalcula listas e contadores. Chamado ao abrir o dashboard e sempre que os dados são gravados.</summary>
    public void Refresh()
    {
        var list = Main.Connections;

        TotalCount = list.Count;
        Summary = BuildSummary(list);

        Reset(Recent, list
            .Where(c => c.LastConnected is not null)
            .OrderByDescending(c => c.LastConnected)
            .Take(MaxRecent));

        Reset(Favorites, list
            .Where(c => c.IsFavorite)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));

        OnPropertyChanged(nameof(Greeting));
    }

    private static string BuildSummary(IReadOnlyList<Connection> list)
    {
        if (list.Count == 0)
            return "Nenhuma conexão cadastrada";

        var groups = list
            .Select(c => c.Group.Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Count();
        var rdp = list.Count(c => c.Protocol == ConnectionProtocol.Rdp);
        var ssh = list.Count - rdp;

        var parts = new List<string>
        {
            (list.Count == 1 ? "1 conexão" : $"{list.Count} conexões")
                + groups switch { 0 => "", 1 => " em 1 grupo", _ => $" em {groups} grupos" },
        };
        if (rdp > 0) parts.Add($"{rdp} RDP");
        if (ssh > 0) parts.Add($"{ssh} SSH");
        return string.Join(" · ", parts);
    }

    private static void Reset(ObservableCollection<Connection> target, IEnumerable<Connection> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
