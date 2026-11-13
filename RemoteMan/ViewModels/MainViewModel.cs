using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;
using RemoteMan.Services;
using Windows.ApplicationModel.DataTransfer;

namespace RemoteMan.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string NoGroup = "Sem grupo";

    // salvamento automático: grava em disco pouco depois da última edição
    private static readonly TimeSpan AutoSaveDelay = TimeSpan.FromMilliseconds(800);
    // mensagens de sucesso/informação somem sozinhas; avisos e erros ficam até o usuário fechar
    private static readonly TimeSpan StatusAutoClose = TimeSpan.FromSeconds(5);

    // verificação de status (teste de porta TCP)
    private const int MaxParallelChecks = 16;
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(1);

    // propriedades que mudam a posição/texto do item na lista agrupada
    private static readonly HashSet<string> ListProperties =
        [nameof(Connection.Name), nameof(Connection.Group), nameof(Connection.Host), nameof(Connection.Username)];

    private readonly ConnectionStore _store = new();
    private readonly List<Connection> _connections = [];
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly DispatcherQueueTimer _autoSaveTimer;
    private readonly DispatcherQueueTimer _statusTimer;
    private CancellationTokenSource? _checkCts;
    private bool _listDirty;

    public MainViewModel()
    {
        SearchText = string.Empty;
        Password = string.Empty;
        StatusMessage = string.Empty;
        StatusSummary = "Status não verificado";

        _autoSaveTimer = _dispatcher.CreateTimer();
        _autoSaveTimer.Interval = AutoSaveDelay;
        _autoSaveTimer.IsRepeating = false;
        _autoSaveTimer.Tick += (_, _) => SaveToDisk();

        _statusTimer = _dispatcher.CreateTimer();
        _statusTimer.Interval = StatusAutoClose;
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => IsStatusOpen = false;

        try
        {
            _connections = _store.Load();
        }
        catch (Exception ex)
        {
            ShowStatus($"Não foi possível ler {_store.FilePath}: {ex.Message}", InfoBarSeverity.Error);
        }

        RefreshGroups();
    }

    /// <summary>Disparado sempre que as conexões são gravadas em disco (o dashboard se atualiza).</summary>
    public event EventHandler? DataChanged;

    public ObservableCollection<ConnectionGroup> Groups { get; } = [];

    /// <summary>Todas as conexões (sem filtro de busca), usadas pelo dashboard.</summary>
    public IReadOnlyList<Connection> Connections => _connections;

    public string DataFilePath => _store.FilePath;

    /// <summary>True enquanto a lista é reconstruída (a View ignora mudanças de seleção nesse período).</summary>
    public bool IsRefreshing { get; private set; }

    [ObservableProperty] public partial Connection? Selected { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }

    /// <summary>Painel de detalhes em modo de edição (false = somente leitura).</summary>
    [ObservableProperty] public partial bool IsEditing { get; set; }

    /// <summary>Senha digitada e ainda não gravada no Credential Manager.</summary>
    [ObservableProperty] public partial string Password { get; set; }
    [ObservableProperty] public partial bool HasSavedPassword { get; set; }

    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; }

    // --- Status online/offline ---
    [ObservableProperty] public partial bool IsChecking { get; set; }
    [ObservableProperty] public partial DateTime? LastCheckedAt { get; set; }

    /// <summary>Resumo em uma frase, ex.: "12 online · 2 offline".</summary>
    [ObservableProperty] public partial string StatusSummary { get; set; }

    partial void OnSearchTextChanged(string value) => RefreshGroups();

    partial void OnSelectedChanged(Connection? oldValue, Connection? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= Selected_PropertyChanged;
            // senha digitada na conexão anterior não se perde ao trocar de item
            TryPersistPendingPassword(oldValue);
            FlushAutoSave();
        }

        if (newValue is not null)
            newValue.PropertyChanged += Selected_PropertyChanged;

        Password = string.Empty;
        HasSavedPassword = newValue is not null && CredentialStore.Exists(CredentialStore.KeyFor(newValue));
        IsEditing = false;

        // nome/grupo editados: reorganiza a lista depois que a troca de seleção terminar
        if (_listDirty)
            _dispatcher.TryEnqueue(RefreshGroups);
    }

    private void Selected_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Connection.Status))
            return;

        if (e.PropertyName is { } name && ListProperties.Contains(name))
            _listDirty = true;

        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    // --- Comandos de criação e edição ---

    /// <summary>Grupos já usados (sugestões no diálogo de nova conexão).</summary>
    public IReadOnlyList<string> GroupNames =>
        _connections
            .Select(c => c.Group.Trim())
            .Where(g => g.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Adiciona uma conexão vinda do diálogo "Nova conexão" e a seleciona (modo leitura).</summary>
    public void AddConnection(Connection c, string? password, bool connect)
    {
        _connections.Add(c);

        // grava a senha antes de selecionar, para o painel já mostrar "Salva no Windows"
        var passwordFailed = false;
        if (!string.IsNullOrEmpty(password))
        {
            try
            {
                CredentialStore.Write(CredentialStore.KeyFor(c), c.Username, password);
            }
            catch (Exception ex)
            {
                passwordFailed = true;
                ShowStatus($"Conexão criada, mas a senha não foi salva: {ex.Message}", InfoBarSeverity.Warning);
            }
        }

        SearchText = string.Empty; // garante que a nova apareça na lista
        RefreshGroups();
        Selected = c;
        IsEditing = false;
        if (!SaveToDisk())
            return;

        if (connect)
            ConnectTo(c);
        else if (!passwordFailed)
            ShowStatus($"\"{c.Name}\" criada.", InfoBarSeverity.Success);
    }

    /// <summary>Conexão no formato neutro de importação/exportação (sem senha).</summary>
    public static ImportedConnection ToImported(Connection c) => new()
    {
        Protocol = c.Protocol,
        Name = c.Name,
        Host = c.Host.Trim(),
        Port = (int)c.Port,
        Username = c.Username,
        Domain = c.Domain,
        Group = c.Group,
        Notes = c.Notes,
        IsFavorite = c.IsFavorite,
        UseWindowsCredentials = c.UseWindowsCredentials,
        KeyFile = c.KeyFile,
        JumpHost = c.JumpHost,
        FullScreen = c.FullScreen,
        DesktopWidth = (int)c.DesktopWidth,
        DesktopHeight = (int)c.DesktopHeight,
        MultiMonitor = c.MultiMonitor,
        RedirectClipboard = c.RedirectClipboard,
        RedirectDrives = c.RedirectDrives,
        AdminSession = c.AdminSession,
        GatewayHost = c.GatewayHost,
        GatewayBypassLocal = c.GatewayBypassLocal,
        GatewayUseSameCredentials = c.GatewayUseSameCredentials,
    };

    /// <summary>Conexões de um grupo da lista ("Sem grupo" = conexões sem grupo).</summary>
    public IReadOnlyList<Connection> ConnectionsInGroup(string groupKey) =>
        _connections
            .Where(c => groupKey == NoGroup
                ? string.IsNullOrWhiteSpace(c.Group)
                : string.Equals(c.Group.Trim(), groupKey, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

    /// <summary>
    /// Adiciona conexões vindas da importação e seleciona a primeira.
    /// Grupo vazio mantém o grupo de origem de cada item (backup do RemoteMan).
    /// </summary>
    public void ImportConnections(IReadOnlyList<ImportedConnection> items, string group)
    {
        if (items.Count == 0)
            return;

        var added = items.Select(i =>
        {
            var c = new Connection
            {
                Protocol = i.Protocol,
                Name = i.Name,
                Group = string.IsNullOrWhiteSpace(group) ? i.Group : group,
                Notes = i.Notes,
                IsFavorite = i.IsFavorite,
                Host = i.Host,
                Port = i.Port,
                Username = i.Username,
                Domain = i.Domain,
                KeyFile = i.KeyFile,
                JumpHost = i.JumpHost,
                GatewayHost = i.GatewayHost,
            };
            // opções RDP ausentes no arquivo mantêm o padrão do app
            if (i.FullScreen is { } fullScreen) c.FullScreen = fullScreen;
            if (i.DesktopWidth is { } width) c.DesktopWidth = width;
            if (i.DesktopHeight is { } height) c.DesktopHeight = height;
            if (i.MultiMonitor is { } multiMonitor) c.MultiMonitor = multiMonitor;
            if (i.RedirectClipboard is { } clipboard) c.RedirectClipboard = clipboard;
            if (i.RedirectDrives is { } drives) c.RedirectDrives = drives;
            if (i.AdminSession is { } admin) c.AdminSession = admin;
            if (i.UseWindowsCredentials is { } windows) c.UseWindowsCredentials = windows;
            if (i.GatewayBypassLocal is { } bypass) c.GatewayBypassLocal = bypass;
            if (i.GatewayUseSameCredentials is { } sameCredentials) c.GatewayUseSameCredentials = sameCredentials;
            return c;
        }).ToList();

        _connections.AddRange(added);
        SearchText = string.Empty;
        RefreshGroups();
        Selected = added[0];
        IsEditing = false;
        if (SaveToDisk())
            ShowStatus(added.Count == 1 ? $"\"{added[0].Name}\" importada." : $"{added.Count} conexões importadas.", InfoBarSeverity.Success);
        _ = CheckStatusAsync(); // já mostra quais servidores importados respondem
    }

    [RelayCommand]
    private void Edit()
    {
        if (Selected is not null)
            IsEditing = true;
    }

    /// <summary>Sai do modo de edição gravando tudo (senha pendente, arquivo e lista).</summary>
    [RelayCommand]
    private void FinishEditing()
    {
        if (Selected is { } c && !TryPersistPendingPassword(c))
            return;

        FlushAutoSave();
        if (_listDirty)
            RefreshGroups();
        IsEditing = false;
    }

    /// <summary>Ctrl+S: grava imediatamente, sem sair do modo de edição.</summary>
    [RelayCommand]
    private void Save()
    {
        if (Selected is { } c && !TryPersistPendingPassword(c))
            return;

        _autoSaveTimer.Stop();
        if (SaveToDisk())
        {
            if (_listDirty)
                RefreshGroups();
            ShowStatus("Conexões salvas.", InfoBarSeverity.Success);
        }
    }

    [RelayCommand]
    private void Duplicate()
    {
        if (Selected is not { } source)
            return;

        var copy = source.Clone();
        copy.Name = $"{source.Name} (cópia)";
        _connections.Add(copy);

        try
        {
            var pwd = CredentialStore.ReadPassword(CredentialStore.KeyFor(source));
            if (pwd is not null)
                CredentialStore.Write(CredentialStore.KeyFor(copy), copy.Username, pwd);
        }
        catch (Exception ex)
        {
            ShowStatus($"Cópia criada, mas a senha não foi copiada: {ex.Message}", InfoBarSeverity.Warning);
        }

        RefreshGroups();
        Selected = copy;
        IsEditing = true; // a cópia normalmente precisa de outro nome/host
        SaveToDisk();
    }

    /// <summary>Chamado pela View após a confirmação do usuário.</summary>
    public void DeleteSelected()
    {
        if (Selected is not { } c)
            return;

        try
        {
            CredentialStore.Delete(CredentialStore.KeyFor(c));
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao remover a senha salva: {ex.Message}", InfoBarSeverity.Warning);
        }

        _connections.Remove(c);
        Password = string.Empty; // evita que a senha pendente seja gravada para um item excluído
        Selected = null;
        RefreshGroups();
        if (SaveToDisk())
            ShowStatus($"\"{c.Name}\" excluída.", InfoBarSeverity.Success);
    }

    // --- Conectar ---

    [RelayCommand]
    private void Connect() => ConnectTo(Selected);

    /// <summary>Abre a conexão informada (usada pelos detalhes, pela lista e pelo dashboard). Retorna se abriu.</summary>
    public bool ConnectTo(Connection? c)
    {
        if (c is null)
            return false;

        if (string.IsNullOrWhiteSpace(c.Host))
        {
            ShowStatus($"Informe o host de \"{c.Name}\" antes de conectar.", InfoBarSeverity.Warning);
            return false;
        }

        // a senha digitada no editor pertence à conexão selecionada, não necessariamente a "c"
        if (Selected is { } editing && !TryPersistPendingPassword(editing))
            return false;

        try
        {
            var password = CredentialStore.ReadPassword(CredentialStore.KeyFor(c));

            if (c.Protocol == ConnectionProtocol.Rdp)
            {
                RdpLauncher.Launch(c, password);
                ShowStatus($"Abrindo {c.Name} no Conexão de Área de Trabalho Remota…", InfoBarSeverity.Success);
            }
            else
            {
                SshLauncher.Launch(c);
                // o ssh.exe não aceita senha por argumento: deixa ela pronta para colar
                if (password is not null && string.IsNullOrWhiteSpace(c.KeyFile))
                {
                    CopyToClipboard(password);
                    ShowStatus($"Abrindo {c.Name}. Senha copiada — cole com clique direito no terminal.", InfoBarSeverity.Success);
                }
                else
                {
                    ShowStatus($"Abrindo {c.Name} no terminal…", InfoBarSeverity.Success);
                }
            }

            c.LastConnected = DateTime.Now;
            c.ConnectCount++;
            _autoSaveTimer.Stop();
            SaveToDisk();
            return true;
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao abrir a conexão: {ex.Message}", InfoBarSeverity.Error);
            return false;
        }
    }

    /// <summary>Abre todas as conexões informadas (ex.: um grupo inteiro). Ignora as que não têm host.</summary>
    public void ConnectAll(IReadOnlyList<Connection> items, string groupName)
    {
        var targets = items.Where(c => !string.IsNullOrWhiteSpace(c.Host)).ToList();
        var skipped = items.Count - targets.Count;
        var failed = targets.Count(c => !ConnectTo(c));
        var opened = targets.Count - failed;

        var message = $"Abrindo {opened} {(opened == 1 ? "conexão" : "conexões")} de \"{groupName}\".";
        if (skipped > 0)
            message += $" {skipped} sem host {(skipped == 1 ? "foi ignorada" : "foram ignoradas")}.";
        if (failed > 0)
            message += $" {failed} {(failed == 1 ? "falhou" : "falharam")}.";
        // várias SSH com senha: a área de transferência fica só com a última
        if (targets.Count(c => c.Protocol == ConnectionProtocol.Ssh && string.IsNullOrWhiteSpace(c.KeyFile)) > 1)
            message += " Senhas SSH: a área de transferência guarda só a da última.";

        ShowStatus(message, skipped + failed > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        if (Selected is not { } c)
            return;

        c.IsFavorite = !c.IsFavorite;
        _autoSaveTimer.Stop();
        SaveToDisk();
    }

    /// <summary>Seleciona uma conexão (em modo leitura) garantindo que ela apareça na lista.</summary>
    public void Reveal(Connection c)
    {
        if (!Groups.Any(g => g.Contains(c)))
            SearchText = string.Empty;
        Selected = c;
        IsEditing = false;
    }

    // --- Senha ---

    [RelayCommand]
    private void CopyPassword()
    {
        if (Selected is not { } c)
            return;

        var pwd = !string.IsNullOrEmpty(Password) ? Password : CredentialStore.ReadPassword(CredentialStore.KeyFor(c));
        if (pwd is null)
        {
            ShowStatus("Nenhuma senha salva para esta conexão.", InfoBarSeverity.Warning);
            return;
        }

        CopyToClipboard(pwd);
        ShowStatus("Senha copiada para a área de transferência.", InfoBarSeverity.Success);
    }

    [RelayCommand]
    private void ClearPassword()
    {
        if (Selected is not { } c)
            return;

        try
        {
            CredentialStore.Delete(CredentialStore.KeyFor(c));
            Password = string.Empty;
            HasSavedPassword = false;
            ShowStatus("Senha salva removida.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao remover a senha: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    // --- Status online/offline ---

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

        var targets = _connections.Where(c => !string.IsNullOrWhiteSpace(c.Host)).ToList();
        foreach (var c in _connections)
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
        var online = _connections.Count(c => c.Status == ReachabilityStatus.Online);
        var offline = _connections.Count(c => c.Status == ReachabilityStatus.Offline);
        var checking = _connections.Count(c => c.Status == ReachabilityStatus.Checking);

        StatusSummary = checking > 0
            ? $"Verificando {checking} {(checking == 1 ? "servidor" : "servidores")}…"
            : online + offline == 0
                ? "Status não verificado"
                : offline == 0
                    ? online == 1 ? "1 servidor online" : $"Todos os {online} servidores online"
                    : $"{online} online · {offline} offline";
    }

    // --- Atualizações ---

    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromDays(1);

    /// <summary>Release mais novo que a versão em uso (null = nenhum encontrado).</summary>
    [ObservableProperty] public partial ReleaseInfo? AvailableUpdate { get; set; }
    [ObservableProperty] public partial bool IsUpdateOpen { get; set; }

    public string UpdateMessage => AvailableUpdate is { } u
        ? $"A versão {u.Version} está disponível (você usa a {UpdateChecker.CurrentVersion})."
        : string.Empty;

    partial void OnAvailableUpdateChanged(ReleaseInfo? value) => OnPropertyChanged(nameof(UpdateMessage));

    /// <summary>Verificação automática ao abrir: respeita a preferência e o intervalo, e falha em silêncio.</summary>
    public async Task CheckForUpdatesIfDueAsync()
    {
        var settings = App.Settings;
        if (!settings.CheckForUpdates)
            return;
        if (settings.LastUpdateCheck is { } last && DateTime.Now - last < UpdateCheckInterval)
            return;

        await CheckForUpdatesAsync();
    }

    /// <summary>Consulta o GitHub agora. Retorna uma frase com o resultado (usada em Configurações).</summary>
    public async Task<string> CheckForUpdatesAsync()
    {
        try
        {
            var latest = await UpdateChecker.GetLatestAsync();

            App.Settings.LastUpdateCheck = DateTime.Now;
            try
            {
                App.SettingsStore.Save(App.Settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // só perde o registro da última verificação
            }

            if (latest is not null && UpdateRelease.IsNewer(UpdateChecker.CurrentVersion, latest.Version))
            {
                AvailableUpdate = latest;
                IsUpdateOpen = true;
                return $"Versão {latest.Version} disponível.";
            }

            AvailableUpdate = null;
            IsUpdateOpen = false;
            return $"Você já está na versão mais recente ({UpdateChecker.CurrentVersion}).";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return "Não foi possível verificar agora. Confira a conexão com a internet.";
        }
    }

    /// <summary>Baixa o instalador (ou o zip portátil) da versão nova pelo navegador.</summary>
    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        if (AvailableUpdate is not { } u)
            return;

        var asset = UpdateRelease.PickAsset(u.Assets, UpdateChecker.Architecture, UpdateChecker.IsInstalled);
        await Windows.System.Launcher.LaunchUriAsync(new Uri(asset?.DownloadUrl ?? u.PageUrl));
        if (asset is not null)
            ShowStatus(
                UpdateChecker.IsInstalled
                    ? "Download iniciado. Feche o RemoteMan e abra o instalador para atualizar."
                    : "Download iniciado. Extraia o zip por cima da pasta atual para atualizar.",
                InfoBarSeverity.Informational);
    }

    [RelayCommand]
    private async Task OpenReleaseNotesAsync()
    {
        if (AvailableUpdate is { } u)
            await Windows.System.Launcher.LaunchUriAsync(new Uri(u.PageUrl));
    }

    // --- Persistência ---

    /// <summary>Chamado ao fechar a janela.</summary>
    public void SaveAll()
    {
        if (Selected is { } c)
            TryPersistPendingPassword(c);
        _autoSaveTimer.Stop();
        SaveToDisk();
    }

    /// <summary>Grava agora se houver um salvamento automático pendente.</summary>
    private void FlushAutoSave()
    {
        if (!_autoSaveTimer.IsRunning)
            return;
        _autoSaveTimer.Stop();
        SaveToDisk();
    }

    private bool TryPersistPendingPassword(Connection c)
    {
        if (string.IsNullOrEmpty(Password))
            return true;

        try
        {
            CredentialStore.Write(CredentialStore.KeyFor(c), c.Username, Password);
            Password = string.Empty;
            if (ReferenceEquals(c, Selected))
                HasSavedPassword = true;
            return true;
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao gravar a senha no Credential Manager: {ex.Message}", InfoBarSeverity.Error);
            return false;
        }
    }

    private bool SaveToDisk()
    {
        try
        {
            _store.Save(_connections);
            DataChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao salvar: {ex.Message}", InfoBarSeverity.Error);
            return false;
        }
    }

    private void RefreshGroups()
    {
        _listDirty = false;
        IsRefreshing = true;
        try
        {
            Groups.Clear();
            var term = SearchText?.Trim() ?? string.Empty;

            bool Matches(Connection c) =>
                term.Length == 0
                || c.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || c.Host.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || c.Group.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                || c.Username.Contains(term, StringComparison.CurrentCultureIgnoreCase);

            var groups = _connections
                .Where(Matches)
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Group) ? NoGroup : c.Group.Trim(),
                         StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(g => g.Key == NoGroup)
                .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var g in groups)
                Groups.Add(new ConnectionGroup(g.Key, g.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)));
        }
        finally
        {
            IsRefreshing = false;
        }

        // força a View a re-selecionar o item atual na lista reconstruída
        OnPropertyChanged(nameof(Selected));
    }

    public void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;

        _statusTimer.Stop();
        if (severity is InfoBarSeverity.Success or InfoBarSeverity.Informational)
            _statusTimer.Start();
    }

    private static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        // não deixa a senha no histórico (Win+V) nem sincronizar na nuvem
        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
        {
            IsAllowedInHistory = false,
            IsRoamable = false,
        });
    }
}
