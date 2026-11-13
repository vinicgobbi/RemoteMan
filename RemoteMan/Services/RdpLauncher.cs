using System.Diagnostics;
using System.Text;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Gera um arquivo .rdp temporário e abre o mstsc.exe.</summary>
public static class RdpLauncher
{
    /// <summary>Opções do .rdp para uma conexão (usadas ao conectar e ao exportar).</summary>
    public static RdpFileOptions OptionsFor(Connection c, string? username) => new()
    {
        Host = c.Host.Trim(),
        Port = (int)c.Port,
        Username = username,
        FullScreen = c.FullScreen,
        DesktopWidth = (int)c.DesktopWidth,
        DesktopHeight = (int)c.DesktopHeight,
        MultiMonitor = c.MultiMonitor,
        RedirectClipboard = c.RedirectClipboard,
        RedirectDrives = c.RedirectDrives,
        GatewayHost = c.GatewayHost,
        GatewayBypassLocal = c.GatewayBypassLocal,
        GatewayUseSameCredentials = c.GatewayUseSameCredentials,
    };

    /// <summary>Usuário gravado no .rdp exportado (sem o usuário do Windows, que é de quem abrir o arquivo).</summary>
    public static string? ExportUsername(Connection c) =>
        c.UseWindowsCredentials || string.IsNullOrWhiteSpace(c.Username) ? null : c.FullUsername;

    public static void Launch(Connection c, string? password)
    {
        var host = c.Host.Trim();

        string? username;
        if (c.UseWindowsCredentials)
        {
            // Usuário logado no Windows. Remove a credencial genérica TERMSRV/host que o próprio app
            // possa ter gravado antes; senão o mstsc a usaria no lugar do usuário do Windows.
            // (Credenciais do "Lembrar de mim" do mstsc são de outro tipo e não são tocadas.)
            username = Connection.CurrentWindowsUser;
            CredentialStore.Delete(CredentialStore.RdpKeyFor(host));
        }
        else
        {
            username = string.IsNullOrWhiteSpace(c.Username) ? null : c.FullUsername;

            // Grava a credencial onde o mstsc procura (TERMSRV/host) para conectar sem pedir senha.
            if (password is not null && username is not null)
                CredentialStore.Write(CredentialStore.RdpKeyFor(host), username, password);
        }

        var content = RdpFileBuilder.Build(OptionsFor(c, username));

        var dir = Path.Combine(Path.GetTempPath(), "RemoteMan");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{c.Id}.rdp");
        File.WriteAllText(file, content, Encoding.Unicode);

        var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "mstsc.exe"))
        {
            UseShellExecute = false,
        };
        psi.ArgumentList.Add(file);
        if (c.AdminSession)
            psi.ArgumentList.Add("/admin");

        Process.Start(psi);
    }
}
