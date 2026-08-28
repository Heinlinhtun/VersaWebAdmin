using System.Threading.Channels;

namespace VersaWebAdmin.Services;

public sealed class ManualBackupQueue : IManualBackupQueue
{
    private readonly Channel<bool> _queue = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false
        });

    private int _isPendingOrRunning;

    public bool TryQueueBackup()
    {
        if (Interlocked.CompareExchange(ref _isPendingOrRunning, 1, 0) != 0)
        {
            return false;
        }

        if (_queue.Writer.TryWrite(true))
        {
            return true;
        }

        Interlocked.Exchange(ref _isPendingOrRunning, 0);
        return false;
    }

    public async ValueTask WaitForBackupAsync(CancellationToken cancellationToken)
    {
        await _queue.Reader.ReadAsync(cancellationToken);
    }

    public void MarkBackupCompleted()
    {
        Interlocked.Exchange(ref _isPendingOrRunning, 0);
    }
}
