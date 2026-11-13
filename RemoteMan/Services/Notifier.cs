using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using RemoteMan.Models;

namespace RemoteMan.Services;

/// <summary>Notificações do Windows (Windows App SDK) para quedas e retornos de servidores favoritos.</summary>
public static class Notifier
{
    private const string ConnectionArg = "connection";
    private const int MaxIndividual = 3;

    private static bool _registered;

    /// <summary>Chamado ao clicar numa notificação, com o Id da conexão (null = resumo de várias).</summary>
    public static event Action<Guid?>? Invoked;

    public static void Initialize()
    {
        try
        {
            if (!AppNotificationManager.IsSupported())
                return;

            // o evento precisa ser assinado antes do Register
            AppNotificationManager.Default.NotificationInvoked += (_, args) =>
            {
                Guid? id = args.Arguments.TryGetValue(ConnectionArg, out var value) && Guid.TryParse(value, out var g) ? g : null;
                Invoked?.Invoke(id);
            };
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception)
        {
            // sem notificações (ex.: política do Windows); o resto do app segue normal
            _registered = false;
        }
    }

    public static void Unregister()
    {
        if (!_registered)
            return;
        try
        {
            AppNotificationManager.Default.Unregister();
        }
        catch (Exception)
        {
            // encerrando o app: nada a fazer
        }
    }

    public static void ShowStatusChanges(IReadOnlyList<(Connection Connection, bool IsOnline)> changes)
    {
        if (!_registered || changes.Count == 0)
            return;

        if (changes.Count > MaxIndividual)
        {
            var down = changes.Count(c => !c.IsOnline);
            var up = changes.Count - down;
            Show(new AppNotificationBuilder()
                .AddText($"{changes.Count} servidores favoritos mudaram de estado")
                .AddText($"{down} offline · {up} de volta"));
            return;
        }

        foreach (var (c, online) in changes)
        {
            Show(new AppNotificationBuilder()
                .AddArgument(ConnectionArg, c.Id.ToString())
                .AddText(online ? $"{c.Name} está de volta" : $"{c.Name} está offline")
                .AddText(online
                    ? $"{c.Host.Trim()}:{(int)c.Port} voltou a responder."
                    : $"{c.Host.Trim()}:{(int)c.Port} não responde."));
        }
    }

    private static void Show(AppNotificationBuilder builder)
    {
        try
        {
            AppNotificationManager.Default.Show(builder.BuildNotification());
        }
        catch (Exception)
        {
            // falha ao exibir uma notificação não deve afetar o monitoramento
        }
    }
}
