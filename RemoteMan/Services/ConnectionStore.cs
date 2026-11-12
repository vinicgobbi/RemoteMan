using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Persiste as conexões em %LOCALAPPDATA%\RemoteMan\connections.json.</summary>
public sealed class ConnectionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RemoteMan",
        "connections.json");

    public List<Connection> Load()
    {
        if (!File.Exists(FilePath))
            return [];

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<Connection>>(json, Options) ?? [];
    }

    public void Save(IEnumerable<Connection> connections)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        // grava num temporário e troca, para não corromper o arquivo se algo falhar no meio
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(connections, Options));
        File.Move(tmp, FilePath, overwrite: true);
    }
}
