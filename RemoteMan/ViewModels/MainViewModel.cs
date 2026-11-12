using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;
using RemoteMan.Services;
using Windows.ApplicationModel.DataTransfer;

namespace RemoteMan.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const string NoGroup = "Sem grupo";

    private readonly ConnectionStore _store = new();
    private readonly List<Connection> _connections = [];

    public MainViewModel()
    {
        SearchText = string.Empty;
        Password = string.Empty;
        StatusMessage = string.Empty;

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

    public ObservableCollection<ConnectionGroup> Groups { get; } = [];

    /// <summary>True enquanto a lista é reconstruída (a View ignora mudanças de seleção nesse período).</summary>
    public bool IsRefreshing { get; private set; }

    [ObservableProperty] public partial Connection? Selected { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; }

    /// <summary>Senha digitada e ainda não gravada no Credential Manager.</summary>
    [ObservableProperty] public partial string Password { get; set; }
    [ObservableProperty] public partial bool HasSavedPassword { get; set; }

    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial InfoBarSeverity StatusSeverity { get; set; }

    partial void OnSearchTextChanged(string value) => RefreshGroups();

    partial void OnSelectedChanged(Connection? oldValue, Connection? newValue)
    {
        // senha digitada na conexão anterior não se perde ao trocar de item
        if (oldValue is not null)
            TryPersistPendingPassword(oldValue);

        Password = string.Empty;
        HasSavedPassword = newValue is not null && CredentialStore.Exists(CredentialStore.KeyFor(newValue));
    }

    [RelayCommand]
    private void NewRdp() => AddNew(ConnectionProtocol.Rdp);

    [RelayCommand]
    private void NewSsh() => AddNew(ConnectionProtocol.Ssh);

    private void AddNew(ConnectionProtocol protocol)
    {
        var c = Connection.Create(protocol);
        c.Group = Selected?.Group ?? string.Empty;
        _connections.Add(c);

        SearchText = string.Empty; // garante que a nova apareça (dispara RefreshGroups)
        RefreshGroups();
        Selected = c;
        SaveToDisk();
        ShowStatus("Conexão criada. Preencha os dados e clique em Salvar.", InfoBarSeverity.Informational);
    }

    [RelayCommand]
    private void Save()
    {
        if (Selected is { } c && !TryPersistPendingPassword(c))
            return;

        if (SaveToDisk())
        {
            RefreshGroups(); // o grupo/nome pode ter mudado
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

    [RelayCommand]
    private void Connect()
    {
        if (Selected is not { } c)
            return;

        if (string.IsNullOrWhiteSpace(c.Host))
        {
            ShowStatus("Informe o host antes de conectar.", InfoBarSeverity.Warning);
            return;
        }

        if (!TryPersistPendingPassword(c))
            return;

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
            SaveToDisk();
        }
        catch (Exception ex)
        {
            ShowStatus($"Falha ao abrir a conexão: {ex.Message}", InfoBarSeverity.Error);
        }
    }

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

    /// <summary>Chamado ao fechar a janela.</summary>
    public void SaveAll()
    {
        if (Selected is { } c)
            TryPersistPendingPassword(c);
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

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
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
