using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;
using RemoteMan.Services;

namespace RemoteMan.Views;

/// <summary>Informações do ambiente (OpenSSH, Windows Terminal, arquivo de dados) e sobre o app.</summary>
public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public string WindowsUser { get; } = Connection.CurrentWindowsUser;
    public string? SshPath { get; } = SshLauncher.FindSshPath();
    public bool HasWindowsTerminal { get; } = SshLauncher.FindWindowsTerminal() is not null;
    public string DataFilePath => App.ViewModel.DataFilePath;

    public string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "—";
}
