namespace TaskMachines.Common.Async.Locks
{
    public class AsyncSemaphore : IDisposable
    {
        private int _currentCount; // Current number of available slots
        private readonly int _maxCount;
        private readonly LinkedList<TaskCompletionSource<bool>> _waitQueue = [];
        private readonly object _lock = new();

        public AsyncSemaphore(int initialCount, int maxCount)
        {
            if (initialCount < 0 || maxCount <= 0 || initialCount > maxCount)
                throw new ArgumentOutOfRangeException(nameof(initialCount));

            _currentCount = initialCount;
            _maxCount = maxCount;
        }

        public void Wait()
        {
            lock (_lock)
            {
                while (_currentCount == 0)
                {
                    Monitor.Wait(_lock); // Block the thread until a slot is available
                }

                _currentCount--; // Take a slot
            }
        }

        public Task<bool> WaitAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (_currentCount > 0)
                {
                    _currentCount--;
                    return Task.FromResult(true);
                }

                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _waitQueue.AddLast(tcs);

                return timeout == null ? tcs.Task : WaitWithTimeoutAsync(tcs, (TimeSpan)timeout, cancellationToken);
            }
        }

        private async Task<bool> WaitWithTimeoutAsync(TaskCompletionSource<bool> tcs, TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            if (timeout.TotalMilliseconds > 0)
                timeoutCts.CancelAfter(timeout);

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, linkedCts.Token));

                return completedTask == tcs.Task || ReleaseWaitingTask(tcs); // First condition: acquire the lock, second condition: release the task
            }
            catch (TaskCanceledException)
            {
                return ReleaseWaitingTask(tcs);
            }
        }

        private bool ReleaseWaitingTask(TaskCompletionSource<bool> tcs)
        {
            lock (_lock)
            {
                _waitQueue.Remove(tcs);
            }

            return false;
        }

        public void Release()
        {
            lock (_lock)
            {
                if (_currentCount == _maxCount)
                    throw new SemaphoreFullException("Cannot release beyond max count");

                if (_waitQueue.Count > 0)
                {
                    var tcs = _waitQueue.First?.Value;
                    tcs?.SetResult(true); // awake an asynchronous waiter
                    _waitQueue.RemoveFirst();
                }
                else
                {
                    _currentCount++;
                    Monitor.Pulse(_lock); // awake a synchronous waiter
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
