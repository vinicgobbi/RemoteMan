using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RemoteMan.Models;
using RemoteMan.Services;

namespace RemoteMan.ViewModels;

/// <summary>Dados da tela inicial, calculados a partir das conexões do <see cref="MainViewModel"/>.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private const int MaxParallelChecks = 16;
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    private readonly MainViewModel _main;
    private CancellationTokenSource? _checkCts;

    public DashboardViewModel(MainViewModel main)
    {
        _main = main;
        OnlineSummary = "—";
        StatusDetail = "Não verificado";
    }

    public ObservableCollection<Connection> Recent { get; } = [];
    public ObservableCollection<Connection> Favorites { get; } = [];
    public ObservableCollection<Connection> MostUsed { get; } = [];
    public ObservableCollection<Connection> All { get; } = [];

    [ObservableProperty] public partial int TotalCount { get; set; }
    [ObservableProperty] public partial int RdpCount { get; set; }
    [ObservableProperty] public partial int SshCount { get; set; }
    [ObservableProperty] public partial int GroupCount { get; set; }
    [ObservableProperty] public partial Connection? LastConnection { get; set; }

    [ObservableProperty] public partial bool IsChecking { get; set; }
    [ObservableProperty] public partial DateTime? LastCheckedAt { get; set; }
    [ObservableProperty] public partial string OnlineSummary { get; set; }
    [ObservableProperty] public partial string StatusDetail { get; set; }

    // --- Ambiente ---
    public string WindowsUser { get; } = Connection.CurrentWindowsUser;
    public string? SshPath { get; } = SshLauncher.FindSshPath();
    public bool HasWindowsTerminal { get; } = SshLauncher.FindWindowsTerminal() is not null;
    public string DataFilePath => _main.DataFilePath;

    public string Greeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var period = hour < 12 ? "Bom dia" : hour < 18 ? "Boa tarde" : "Boa noite";
            var user = WindowsUser.Split('\\').Last();
            return $"{period}, {user}";
        }
    }

    /// <summary>Recalcula listas e contadores. Chamado ao abrir o dashboard e após conectar.</summary>
    public void Refresh()
    {
        var list = _main.Connections;

        TotalCount = list.Count;
        RdpCount = list.Count(c => c.Protocol == ConnectionProtocol.Rdp);
        SshCount = list.Count(c => c.Protocol == ConnectionProtocol.Ssh);
        GroupCount = list
            .Select(c => c.Group.Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Count();

        Reset(Recent, list
            .Where(c => c.LastConnected is not null)
            .OrderByDescending(c => c.LastConnected)
            .Take(8));
        LastConnection = Recent.FirstOrDefault();

        Reset(Favorites, list
            .Where(c => c.IsFavorite)
            .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));

        Reset(MostUsed, list
            .Where(c => c.ConnectCount > 0)
            .OrderByDescending(c => c.ConnectCount)
            .ThenByDescending(c => c.LastConnected)
            .Take(5));

        Reset(All, list
            .OrderBy(c => c.Group, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase));

        UpdateStatusSummary();
        OnPropertyChanged(nameof(Greeting));
    }

    /// <summary>Verifica o status só se a última verificação tiver mais de 1 minuto.</summary>
    public void CheckStatusIfStale()
    {
        if (IsChecking)
            return;
        if (LastCheckedAt is { } last && DateTime.Now - last < StaleAfter)
            return;
        _ = CheckStatusAsync();
    }

    [RelayCommand]
    private async Task CheckStatusAsync()
    {
        _checkCts?.Cancel();
        var cts = _checkCts = new CancellationTokenSource();

        var targets = _main.Connections.Where(c => !string.IsNullOrWhiteSpace(c.Host)).ToList();
        foreach (var c in _main.Connections)
            c.Status = string.IsNullOrWhiteSpace(c.Host) ? ReachabilityStatus.Unknown : ReachabilityStatus.Checking;

        IsChecking = true;
        UpdateStatusSummary();

        using var gate = new SemaphoreSlim(MaxParallelChecks);

        // as continuações voltam para a thread de UI (o método começa nela), então é seguro mexer em Status
        async Task CheckOne(Connection c)
        {
            await gate.WaitAsync(cts.Token);
            try
            {
                var ok = await ReachabilityChecker.IsReachableAsync(c.Host.Trim(), (int)c.Port, CheckTimeout, cts.Token);
                c.Status = ok ? ReachabilityStatus.Online : ReachabilityStatus.Offline;
            }
            finally
            {
                gate.Release();
            }
            UpdateStatusSummary();
        }

        try
        {
            await Task.WhenAll(targets.Select(CheckOne));
            LastCheckedAt = DateTime.Now;
        }
        catch (OperationCanceledException)
        {
            // uma nova verificação começou e assumiu o controle
        }
        finally
        {
            if (ReferenceEquals(_checkCts, cts))
            {
                IsChecking = false;
                UpdateStatusSummary();
            }
        }
    }

    private void UpdateStatusSummary()
    {
        var list = _main.Connections;
        var online = list.Count(c => c.Status == ReachabilityStatus.Online);
        var offline = list.Count(c => c.Status == ReachabilityStatus.Offline);
        var checking = list.Count(c => c.Status == ReachabilityStatus.Checking);
        var tested = online + offline;

        OnlineSummary = tested == 0 && checking == 0 ? "—" : $"{online}/{tested + checking}";
        StatusDetail = checking > 0
            ? $"Verificando {checking}…"
            : tested == 0
                ? "Não verificado"
                : offline == 0 ? "Todos online" : $"{offline} offline";
    }

    private static void Reset(ObservableCollection<Connection> target, IEnumerable<Connection> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }
}
