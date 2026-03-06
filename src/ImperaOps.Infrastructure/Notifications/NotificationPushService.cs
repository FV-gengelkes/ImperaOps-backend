using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ImperaOps.Infrastructure.Notifications;

public sealed class NotificationPushService : INotificationPushService
{
    // Per user: a dictionary of active connections (one per open browser tab)
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Guid, Channel<string>>> _users = new();

    public async IAsyncEnumerable<string> SubscribeAsync(long userId, [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var connId = Guid.NewGuid();
        var userChannels = _users.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<string>>());
        userChannels[connId] = channel;

        try
        {
            await foreach (var msg in channel.Reader.ReadAllAsync(ct))
                yield return msg;
        }
        finally
        {
            userChannels.TryRemove(connId, out _);
        }
    }

    public void Push(long userId, string message)
    {
        if (_users.TryGetValue(userId, out var channels))
        {
            foreach (var (_, ch) in channels)
                ch.Writer.TryWrite(message);
        }
    }
}
