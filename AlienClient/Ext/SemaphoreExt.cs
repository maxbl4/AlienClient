using System;
using System.Reactive.Disposables;
using System.Threading;

namespace AlienClient.Ext
{
    public static class SemaphoreExt
    {
        public static IDisposable UseOnce(this SemaphoreSlim semaphore)
        {
            semaphore.Wait();
            return Disposable.Create(() => semaphore.Release());
        }
    }
}