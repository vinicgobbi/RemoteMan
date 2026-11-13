using Microsoft.Win32;
using RemoteMan.Models;
using RemoteMan.Services.Importers;

namespace RemoteMan.Services;

/// <summary>De onde importar conexões.</summary>
public enum ImportSource
{
    RdpFiles,
    SshConfig,
    SshConfigFile,
    Putty,
    Backup,
}

/// <summary>Formatos de exportação.</summary>
public enum ExportKind
{
    /// <summary>Backup do RemoteMan (.json, todas as opções, sem senhas).</summary>
    Backup,

    /// <summary>Um arquivo .rdp por conexão RDP.</summary>
    RdpFiles,

    /// <summary>Perfis SSH no formato do ~/.ssh/config.</summary>
    SshConfig,
}

/// <summary>Lê as fontes de importação no disco e no registro e repassa aos parsers.</summary>
public static class ImportSources
{
    private const string PuttySessionsKey = @"Software\SimonTatham\PuTTY\Sessions";

    public static string HomeDir => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>~/.ssh/config do usuário atual.</summary>
    public static string DefaultSshConfigPath => Path.Combine(HomeDir, ".ssh", "config");

    public static ImportResult ReadRdpFiles(IEnumerable<string> paths)
    {
        var connections = new List<ImportedConnection>();
        var errors = new List<string>();

        foreach (var path in paths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            try
            {
                // ReadAllText detecta o BOM (o mstsc grava em UTF-16)
                if (RdpFileParser.TryParse(name, File.ReadAllText(path), out var c, out var error))
                    connections.Add(c);
                else
                    errors.Add(error);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        return new ImportResult(connections, errors);
    }

    public static ImportResult ReadSshConfig(string path)
    {
        if (!File.Exists(path))
            return new ImportResult([], [$"Arquivo não encontrado: {path}"]);

        try
        {
            return SshConfigParser.Parse(File.ReadAllText(path), HomeDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ImportResult([], [$"Não foi possível ler {path}: {ex.Message}"]);
        }
    }

    public static ImportResult ReadBackup(string path)
    {
        try
        {
            return BackupFile.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new ImportResult([], [$"Não foi possível ler {path}: {ex.Message}"]);
        }
    }

    public static ImportResult ReadPuttySessions()
    {
        using var sessions = Registry.CurrentUser.OpenSubKey(PuttySessionsKey);
        if (sessions is null)
            return new ImportResult([], ["Nenhuma sessão do PuTTY encontrada neste usuário."]);

        var connections = new List<ImportedConnection>();
        foreach (var keyName in sessions.GetSubKeyNames())
        {
            using var key = sessions.OpenSubKey(keyName);
            if (key is null)
                continue;

            var values = key.GetValueNames().ToDictionary(n => n, n => key.GetValue(n), StringComparer.OrdinalIgnoreCase);
            if (PuttySessionParser.Parse(keyName, values) is { } c)
                connections.Add(c);
        }

        return new ImportResult(connections, []);
    }
}
