using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RemoteMan.Models;
using RemoteMan.ViewModels;

namespace RemoteMan.Views;

/// <summary>
/// Revisão antes de importar: lista o que foi encontrado, desmarca o que já está cadastrado
/// e permite escolher o grupo de destino.
/// </summary>
public sealed partial class ImportDialog : ContentDialog
{
    private const int MaxErrorsShown = 3;

    private readonly List<ImportItem> _items;

    public ImportDialog(string sourceName, ImportResult result, IReadOnlyList<Connection> existing, IEnumerable<string> groups)
    {
        InitializeComponent();

        _items = result.Connections
            .Select(c => new ImportItem(c, existing.Any(e =>
                e.Protocol == c.Protocol
                && (int)e.Port == c.Port
                && string.Equals(e.Host.Trim(), c.Host, StringComparison.OrdinalIgnoreCase))))
            .ToList();
        ItemsList.ItemsSource = _items;
        GroupBox.ItemsSource = groups.ToList();
        // backup do RemoteMan traz os grupos: campo vazio mantém o grupo de cada conexão
        if (_items.Any(i => i.Connection.Group.Length > 0))
            GroupBox.PlaceholderText = "Manter os grupos do arquivo";

        var total = _items.Count;
        var duplicates = _items.Count(i => i.IsDuplicate);
        SummaryText.Text = $"{total} {(total == 1 ? "conexão encontrada" : "conexões encontradas")} em {sourceName}."
            + (duplicates > 0
                ? $" {duplicates} já {(duplicates == 1 ? "está cadastrada e ficou desmarcada" : "estão cadastradas e ficaram desmarcadas")}."
                : string.Empty);

        if (result.Errors.Count > 0)
        {
            var shown = result.Errors.Take(MaxErrorsShown).ToList();
            var more = result.Errors.Count - shown.Count;
            ErrorsInfo.Title = result.Errors.Count == 1 ? "1 item não pôde ser lido" : $"{result.Errors.Count} itens não puderam ser lidos";
            ErrorsInfo.Message = string.Join("\n", shown) + (more > 0 ? $"\n… e mais {more}." : string.Empty);
            ErrorsInfo.IsOpen = true;
        }
    }

    /// <summary>Conexões marcadas para importar (vazio se cancelado).</summary>
    public IReadOnlyList<ImportedConnection> Selected { get; private set; } = [];

    public string Group { get; private set; } = string.Empty;

    private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        foreach (var item in _items.Where(i => !i.IsDuplicate))
            ItemsList.SelectedItems.Add(item);
        UpdateSelection();
    }

    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelection();

    private void SelectAll_Click(object sender, RoutedEventArgs e) => ItemsList.SelectAll();

    private void SelectNone_Click(object sender, RoutedEventArgs e) => ItemsList.SelectedItems.Clear();

    private void UpdateSelection()
    {
        var count = ItemsList.SelectedItems.Count;
        SelectionText.Text = count == 1 ? "1 selecionada" : $"{count} selecionadas";
        PrimaryButtonText = count > 0 ? $"Importar ({count})" : "Importar";
        IsPrimaryButtonEnabled = count > 0;
    }

    private void Primary_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // mantém a ordem da lista, não a ordem dos cliques
        Selected = _items.Where(ItemsList.SelectedItems.Contains).Select(i => i.Connection).ToList();
        Group = GroupBox.Text?.Trim() ?? string.Empty;
    }
}
