using System.Net.Sockets;

namespace RemoteMan.Services;

/// <summary>Verifica se um host aceita conexão TCP numa porta (ex.: 3389 / 22).</summary>
public static class ReachabilityChecker
{
    public static async Task<bool> IsReachableAsync(string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false; // estourou o timeout
        }
        catch (SocketException)
        {
            return false; // recusado, host inexistente, DNS etc.
        }
    }
}
