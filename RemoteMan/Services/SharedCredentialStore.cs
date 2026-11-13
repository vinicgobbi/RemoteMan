using System.Text.Json;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Persiste as credenciais compartilhadas (sem senhas) em %LOCALAPPDATA%\RemoteMan\credentials.json.</summary>
public sealed class SharedCredentialStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RemoteMan",
        "credentials.json");

    public List<SharedCredential> Load()
    {
        if (!File.Exists(FilePath))
            return [];
        return JsonSerializer.Deserialize<List<SharedCredential>>(File.ReadAllText(FilePath), Options) ?? [];
    }

    public void Save(IEnumerable<SharedCredential> credentials)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(credentials, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
