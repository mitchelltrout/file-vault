namespace FileVault.Service.VaultOperations;

/// <summary>
/// Per-vault exclusive lock. All stream I/O for a vault must be serialized
/// because the underlying FileStream shares a mutable Position across callers.
/// ReadLock/WriteLock are the same mutex — the naming is just for call-site intent.
/// </summary>
public sealed class VaultLock
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public IDisposable ReadLock() => Acquire();
    public IDisposable WriteLock() => Acquire();

    private IDisposable Acquire()
    {
        _mutex.Wait();
        return new Releaser(_mutex);
    }

    private sealed class Releaser(SemaphoreSlim mutex) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            mutex.Release();
        }
    }
}
