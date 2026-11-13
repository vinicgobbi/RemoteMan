namespace RemoteMan.Services;

/// <summary>Campos de uma conexão considerados pela conexão rápida (Ctrl+K).</summary>
public readonly record struct QuickSearchEntry(
    string Name, string Host, string Group, string Username, bool IsFavorite, DateTime? LastConnected);

/// <summary>Busca e ordenação da conexão rápida.</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public static class QuickSearch
{
    public const int MaxResults = 8;

    /// <summary>
    /// Nível de relevância: 0 = nome começa com o termo, 1 = nome contém, 2 = host contém,
    /// 3 = grupo ou usuário contém. Null = não casa. Sem termo, tudo casa com nível 0.
    /// Vários termos (separados por espaço) precisam casar todos; vale o pior nível entre eles.
    /// </summary>
    public static int? Tier(QuickSearchEntry e, string? query)
    {
        var terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var worst = 0;
        foreach (var term in terms)
        {
            int? tier =
                e.Name.StartsWith(term, StringComparison.CurrentCultureIgnoreCase) ? 0
                : e.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase) ? 1
                : e.Host.Contains(term, StringComparison.CurrentCultureIgnoreCase) ? 2
                : e.Group.Contains(term, StringComparison.CurrentCultureIgnoreCase)
                  || e.Username.Contains(term, StringComparison.CurrentCultureIgnoreCase) ? 3
                : null;
            if (tier is null)
                return null;
            worst = Math.Max(worst, tier.Value);
        }
        return worst;
    }

    /// <summary>
    /// Resultados ordenados: relevância, favoritos, uso mais recente e nome.
    /// Sem termo, mostra favoritos e recentes primeiro (e depois o resto, por nome).
    /// </summary>
    public static List<T> Search<T>(IEnumerable<T> items, Func<T, QuickSearchEntry> entry, string? query, int max = MaxResults) =>
        items
            .Select(i => (Item: i, Entry: entry(i), Tier: Tier(entry(i), query)))
            .Where(x => x.Tier is not null)
            .OrderBy(x => x.Tier)
            .ThenByDescending(x => x.Entry.IsFavorite)
            .ThenByDescending(x => x.Entry.LastConnected ?? DateTime.MinValue)
            .ThenBy(x => x.Entry.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(max)
            .Select(x => x.Item)
            .ToList();
}
