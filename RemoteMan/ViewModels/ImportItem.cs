using RemoteMan.Helpers;
using RemoteMan.Models;

namespace RemoteMan.ViewModels;

/// <summary>Linha da lista de revisão do diálogo de importação.</summary>
public sealed class ImportItem(ImportedConnection connection, bool isDuplicate)
{
    public ImportedConnection Connection { get; } = connection;

    /// <summary>Já existe uma conexão com o mesmo protocolo, host e porta.</summary>
    public bool IsDuplicate { get; } = isDuplicate;

    public string Name => Connection.Name;
    public string Glyph => Ui.ProtocolGlyph(Connection.Protocol);
    public string Warning => Connection.Warning ?? string.Empty;

    public string Details
    {
        get
        {
            var text = Ui.Summary(Connection.Protocol, Connection.Host, Connection.Port);
            var user = Connection.Domain.Length > 0 ? $"{Connection.Domain}\\{Connection.Username}" : Connection.Username;
            return user.Length > 0 ? $"{text} · {user}" : text;
        }
    }
}
