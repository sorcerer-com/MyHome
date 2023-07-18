using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyHome.Utils.Tuya;

public static class SemaphoreSlimSimple
{
    public static SemaphoreLock WaitDisposable(this SemaphoreSlim semaphore)
    {
        var l = new SemaphoreLock(semaphore);
        semaphore.Wait();
        //GC.KeepAlive(l);
        return l;
    }

    public static async Task<SemaphoreLock> WaitDisposableAsync(this SemaphoreSlim semaphore, CancellationToken cancellationToken = default)
    {
        var l = new SemaphoreLock(semaphore);
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        //GC.KeepAlive(l);
        return l;
    }
}

public class SemaphoreLock : IDisposable
{
    private readonly SemaphoreSlim lockedSemaphore;

    public SemaphoreLock(SemaphoreSlim lockedSemaphore)
    {
        this.lockedSemaphore = lockedSemaphore;
    }

    public void Dispose()
    {
        this.lockedSemaphore.Release();
    }
}