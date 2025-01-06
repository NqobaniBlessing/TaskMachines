namespace TaskMachines.Common.Async.Locks;

public class AsyncLock(LockConfig? config = null)
{
    private readonly AsyncSemaphore _semaphore = new(1, 1);
    private CancellationTokenSource _timeoutTokenSource = new();
    public LockConfig? Config { get; } = config ?? new LockConfig();

    public async Task<IDisposable> LockAsync()
    {
        var retryCount = 0;

        while (retryCount <= Config!.MaxRetries)
        {
            if (await _semaphore.WaitAsync(Config.Timeout))
            {
                Console.WriteLine("Task acquiring lock");
                StartTimeoutWatchDog(Config.Timeout);
                return new LockHandle(this);
            }

            retryCount++;
            Console.WriteLine(
                $"Task timed out waiting for the lock. Retrying {retryCount}/{Config.MaxRetries}...");

            if (retryCount > Config.MaxRetries)
            {
                throw new TimeoutException(
                    $"Task failed to acquire the lock after {Config.MaxRetries} retries.");
            }

            await Task.Delay(Config.RetryDelay);
        }

        throw new InvalidOperationException();
    }

    private void StartTimeoutWatchDog(TimeSpan timeout)
    {
        _timeoutTokenSource.Dispose();
        _timeoutTokenSource = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(timeout, _timeoutTokenSource.Token);
                Console.WriteLine($"Timeout: Task exceeded the lock holding period. Releasing lock.");
                _semaphore.Release();
            }
            catch (TaskCanceledException)
            {
                // Timeout watchdog was cancelled; no further action needed.
            }
        });
    }

    private void StopTimeoutWatchDog()
    {
        _timeoutTokenSource.Cancel();
    }

    private class LockHandle(AsyncLock asyncLock) : IDisposable
    {
        private bool _isDisposed;

        public void Dispose()
        {
            if (_isDisposed) return;
                
            _isDisposed = true;
            asyncLock.StopTimeoutWatchDog();
            asyncLock._semaphore.Release();
            Console.WriteLine($"Lock released by Task");
        }
    }
}