using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteMan.Models;

namespace RemoteMan.Services.Importers;

/// <summary>
/// Backup do RemoteMan em JSON: todas as opções das conexões, sem senhas.
/// </summary>
public static class BackupFile
{
    public const string FormatName = "remoteman-backup";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record Document(string Format, int Version, DateTime ExportedAt, List<ImportedConnection> Connections);

    public static string Serialize(IEnumerable<ImportedConnection> connections, DateTime exportedAt) =>
        JsonSerializer.Serialize(
            new Document(FormatName, FormatVersion, exportedAt, connections.Select(c => c with { Warning = null }).ToList()),
            Options);

    public static ImportResult Parse(string json)
    {
        Document? doc;
        try
        {
            doc = JsonSerializer.Deserialize<Document>(json, Options);
        }
        catch (JsonException ex)
        {
            return new ImportResult([], [$"O arquivo não é um backup válido do RemoteMan ({ex.Message})."]);
        }

        if (doc is null || doc.Format != FormatName)
            return new ImportResult([], ["O arquivo não é um backup do RemoteMan."]);
        if (doc.Version > FormatVersion)
            return new ImportResult([], [$"Backup gerado por uma versão mais nova do RemoteMan (formato {doc.Version}). Atualize o app."]);

        var valid = new List<ImportedConnection>();
        var errors = new List<string>();
        foreach (var c in doc.Connections ?? [])
        {
            if (string.IsNullOrWhiteSpace(c.Host) || c.Port is < 1 or > 65535)
                errors.Add($"{c.Name}: endereço ou porta inválidos.");
            else
                valid.Add(c with { Warning = null });
        }
        return new ImportResult(valid, errors);
    }
}
