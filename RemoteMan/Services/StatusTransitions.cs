namespace RemoteMan.Services;

/// <summary>Mudança de estado de um servidor entre duas verificações.</summary>
public readonly record struct StatusChange(Guid Id, bool IsOnline);

/// <summary>Detecta quem mudou de online para offline (ou o contrário) entre duas verificações.</summary>
/// <remarks>Classe pura (sem WinUI) para poder ser testada isoladamente.</remarks>
public static class StatusTransitions
{
    /// <param name="previous">Último estado conhecido de cada servidor (true = online).</param>
    /// <param name="current">Resultado da verificação atual.</param>
    /// <returns>Só servidores que já tinham estado conhecido e mudaram (a primeira verificação não gera aviso).</returns>
    public static List<StatusChange> Detect(IReadOnlyDictionary<Guid, bool> previous, IEnumerable<StatusChange> current) =>
        current
            .Where(c => previous.TryGetValue(c.Id, out var was) && was != c.IsOnline)
            .ToList();
}
