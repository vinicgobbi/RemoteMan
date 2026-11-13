using RemoteMan.Services;
using Xunit;

namespace RemoteMan.Tests;

public class StatusTransitionsTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void DetectaQuedaERetorno()
    {
        var previous = new Dictionary<Guid, bool> { [A] = true, [B] = false, [C] = true };
        var changes = StatusTransitions.Detect(previous, [new(A, false), new(B, true), new(C, true)]);
        Assert.Equal([new StatusChange(A, false), new StatusChange(B, true)], changes);
    }

    [Fact]
    public void PrimeiraVerificacaoNaoGeraAviso() =>
        Assert.Empty(StatusTransitions.Detect(new Dictionary<Guid, bool>(), [new(A, false), new(B, true)]));

    [Fact]
    public void ServidorNovoNaListaNaoGeraAviso()
    {
        var previous = new Dictionary<Guid, bool> { [A] = true };
        Assert.Equal([new StatusChange(A, false)], StatusTransitions.Detect(previous, [new(A, false), new(B, false)]));
    }

    [Fact]
    public void SemMudancaNaoGeraAviso()
    {
        var previous = new Dictionary<Guid, bool> { [A] = true, [B] = false };
        Assert.Empty(StatusTransitions.Detect(previous, [new(A, true), new(B, false)]));
    }
}
