using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using RemoteMan.Models;
using RemoteMan.Services;
using RemoteMan.Services.Importers;
using RemoteMan.ViewModels;
using RemoteMan.Views;
using Windows.Graphics;
using Windows.Storage.Pickers;

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
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico")); // barra de tarefas e Alt+Tab
        ApplyBackdrop(App.Settings.Backdrop);
        AppWindow.Resize(new SizeInt32(1280, 820));

        Closed += (_, _) =>
        {
            ViewModel.SaveAll();
            Notifier.Unregister();
        };

        // tela inicial: dashboard
        NavView.SelectedItem = HomeNavItem;
    }

    /// <summary>
    /// Aplica o material de fundo. Se o pedido não for suportado (ex.: Mica no Windows 10), usa o próximo
    /// disponível. Retorna o que foi aplicado (null = nenhum, fundo sólido).
    /// </summary>
    public BackdropKind? ApplyBackdrop(BackdropKind requested)
    {
        var mica = MicaController.IsSupported();
        var acrylic = DesktopAcrylicController.IsSupported();

        BackdropKind? applied = requested switch
        {
            BackdropKind.Acrylic when acrylic => BackdropKind.Acrylic,
            BackdropKind.Acrylic when mica => BackdropKind.Mica,
            _ when mica => requested,
            _ when acrylic => BackdropKind.Acrylic,
            _ => null,
        };

        SystemBackdrop = applied switch
        {
            BackdropKind.Mica => new MicaBackdrop { Kind = MicaKind.Base },
            BackdropKind.MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            BackdropKind.Acrylic => new DesktopAcrylicBackdrop(),
            _ => null,
        };

        // sem material nenhum, a janela precisa de um fundo sólido
        RootGrid.Background = applied is null
            ? (Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"]
            : null;

        AppliedBackdrop = applied;
        return applied;
    }

    /// <summary>Material de fundo em uso (null = nenhum).</summary>
    public BackdropKind? AppliedBackdrop { get; private set; }

    /// <summary>Clique numa notificação: traz a janela para frente e mostra a conexão (se houver).</summary>
    public void ShowFromNotification(Guid? connectionId)
    {
        if (connectionId is { } id && ViewModel.Connections.FirstOrDefault(c => c.Id == id) is { } c)
            ShowConnections(c);
        else
            ShowConnections();
        Activate();
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

    // só um ContentDialog pode ficar aberto por vez
    private bool _dialogOpen;

    /// <summary>Importa conexões da fonte escolhida, com revisão antes de adicionar.</summary>
    public async Task ImportAsync(ImportSource source)
    {
        if (_dialogOpen)
            return;
        _dialogOpen = true;

        try
        {
            ImportResult result;
            string sourceName;
            switch (source)
            {
                case ImportSource.RdpFiles:
                    var files = await PickFilesAsync(multiple: true, ".rdp");
                    if (files.Count == 0)
                        return;
                    result = ImportSources.ReadRdpFiles(files);
                    sourceName = files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} arquivos .rdp";
                    break;

                case ImportSource.SshConfig:
                    result = ImportSources.ReadSshConfig(ImportSources.DefaultSshConfigPath);
                    sourceName = ImportSources.DefaultSshConfigPath;
                    break;

                case ImportSource.SshConfigFile:
                    var picked = await PickFilesAsync(multiple: false, "*");
                    if (picked.Count == 0)
                        return;
                    result = ImportSources.ReadSshConfig(picked[0]);
                    sourceName = picked[0];
                    break;

                case ImportSource.Backup:
                    var backup = await PickFilesAsync(multiple: false, ".json");
                    if (backup.Count == 0)
                        return;
                    result = ImportSources.ReadBackup(backup[0]);
                    sourceName = Path.GetFileName(backup[0]);
                    break;

                default:
                    result = ImportSources.ReadPuttySessions();
                    sourceName = "sessões do PuTTY";
                    break;
            }

            if (result.Connections.Count == 0)
            {
                ViewModel.ShowStatus(
                    result.Errors.Count > 0 ? result.Errors[0] : $"Nenhuma conexão encontrada em {sourceName}.",
                    InfoBarSeverity.Warning);
                return;
            }

            var dialog = new ImportDialog(sourceName, result, ViewModel.Connections, ViewModel.GroupNames)
            {
                XamlRoot = Content.XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Selected.Count == 0)
                return;

            ViewModel.ImportConnections(dialog.Selected, dialog.Group);
            ShowConnections();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    /// <summary>
    /// Exporta conexões (todas ou as informadas, ex.: um grupo). Senhas nunca são exportadas.
    /// </summary>
    /// <param name="scopeName">Usado no nome sugerido do arquivo (ex.: nome do grupo).</param>
    public async Task ExportAsync(ExportKind kind, IReadOnlyList<Connection>? subset = null, string? scopeName = null)
    {
        var source = (subset ?? ViewModel.Connections).Where(c => !string.IsNullOrWhiteSpace(c.Host)).ToList();
        var now = DateTime.Now;
        var suffix = scopeName is null ? now.ToString("yyyy-MM-dd") : $"{scopeName}-{now:yyyy-MM-dd}";

        try
        {
            switch (kind)
            {
                case ExportKind.Backup:
                {
                    if (source.Count == 0)
                        break;
                    var path = await PickSaveFileAsync($"RemoteMan-backup-{suffix}", "Backup do RemoteMan", ".json");
                    if (path is null)
                        return;
                    await File.WriteAllTextAsync(path, BackupFile.Serialize(source.Select(ViewModel.ToImported), now));
                    ViewModel.ShowStatus($"{Plural(source.Count)} {(source.Count == 1 ? "exportada" : "exportadas")} para {Path.GetFileName(path)} (sem senhas).", InfoBarSeverity.Success);
                    return;
                }

                case ExportKind.RdpFiles:
                {
                    source = source.Where(c => c.Protocol == ConnectionProtocol.Rdp).ToList();
                    if (source.Count == 0)
                        break;
                    var folder = await PickFolderAsync();
                    if (folder is null)
                        return;
                    var used = new HashSet<string>(
                        Directory.EnumerateFiles(folder, "*.rdp").Select(Path.GetFileName)!, StringComparer.OrdinalIgnoreCase);
                    foreach (var c in source)
                    {
                        var content = RdpFileBuilder.Build(RdpLauncher.OptionsFor(c, ViewModel.ExportUsername(c)));
                        // UTF-16 com BOM, como o mstsc grava
                        await File.WriteAllTextAsync(Path.Combine(folder, ExportNames.FileName(c.Name, ".rdp", used)), content, System.Text.Encoding.Unicode);
                    }
                    ViewModel.ShowStatus($"{Plural(source.Count)} RDP {(source.Count == 1 ? "exportada" : "exportadas")} como .rdp em {folder} (sem senhas).", InfoBarSeverity.Success);
                    return;
                }

                case ExportKind.SshConfig:
                {
                    source = source.Where(c => c.Protocol == ConnectionProtocol.Ssh).ToList();
                    if (source.Count == 0)
                        break;
                    var path = await PickSaveFileAsync($"ssh_config-remoteman-{suffix}", "Configuração SSH", ".conf");
                    if (path is null)
                        return;
                    await File.WriteAllTextAsync(path, SshConfigWriter.Write(source.Select(ViewModel.ToImported), now));
                    ViewModel.ShowStatus($"{Plural(source.Count)} SSH {(source.Count == 1 ? "exportada" : "exportadas")} para {Path.GetFileName(path)}. Use com \"Include\" no ~/.ssh/config.", InfoBarSeverity.Success);
                    return;
                }
            }

            ViewModel.ShowStatus("Nenhuma conexão para exportar nesse formato.", InfoBarSeverity.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ViewModel.ShowStatus($"Falha ao exportar: {ex.Message}", InfoBarSeverity.Error);
        }

        static string Plural(int n) => n == 1 ? "1 conexão" : $"{n} conexões";
    }

    private static async Task<string?> PickSaveFileAsync(string suggestedName, string typeName, string extension)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add(typeName, [extension]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return (await picker.PickSaveFileAsync())?.Path;
    }

    private static async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));
        return (await picker.PickSingleFolderAsync())?.Path;
    }

    private static async Task<IReadOnlyList<string>> PickFilesAsync(bool multiple, string extension)
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add(extension);
        // app não empacotado: o picker precisa saber qual é a janela dona
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow));

        if (multiple)
            return (await picker.PickMultipleFilesAsync()).Select(f => f.Path).ToList();
        return await picker.PickSingleFileAsync() is { } file ? [file.Path] : [];
    }

    /// <summary>Abre o diálogo "Nova conexão" (botões das telas e Ctrl+N).</summary>
    public async Task NewConnectionAsync()
    {
        if (_dialogOpen)
            return;
        _dialogOpen = true;

        try
        {
            var dialog = new NewConnectionDialog(ViewModel.GroupNames, ViewModel.Selected?.Group, ViewModel.Connections)
            {
                XamlRoot = Content.XamlRoot,
            };
            await dialog.ShowAsync();

            if (dialog.Result is not { } c)
                return;

            ViewModel.AddConnection(c, dialog.Password, dialog.ConnectAfter);
            // "Criar" leva aos detalhes; "Criar e conectar" deixa o usuário onde estava
            if (!dialog.ConnectAfter)
                ShowConnections(c);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async void NewConnectionAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await NewConnectionAsync();
    }

    /// <summary>Abre a conexão rápida (Ctrl+K): busca e conecta sem sair da tela atual.</summary>
    public async Task QuickConnectAsync()
    {
        if (_dialogOpen)
            return;
        _dialogOpen = true;

        try
        {
            var dialog = new QuickConnectDialog(ViewModel.Connections) { XamlRoot = Content.XamlRoot };
            await dialog.ShowAsync();
            if (dialog.Result is { } c)
                ViewModel.ConnectTo(c);
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private async void QuickConnectAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await QuickConnectAsync();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag as string;
        var page = tag switch
        {
            "connections" => typeof(ConnectionsPage),
            "settings" => typeof(SettingsPage),
            _ => typeof(DashboardPage),
        };

        if (ContentFrame.CurrentSourcePageType != page)
            ContentFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
    }
}
