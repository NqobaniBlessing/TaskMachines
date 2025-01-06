using System.Collections.Concurrent;

namespace TaskMachines.Common.Async.ThreadPools
{
    public class CustomThreadPool : IDisposable
    {
        private readonly BlockingCollection<Task> _globalQueue = [];
        private readonly ConcurrentBag<Thread> _workers;
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private volatile bool _isDisposed = false;

        public CustomThreadPool(int minThreads, int maxThreads)
        {
            _minThreads = minThreads;
            _maxThreads = maxThreads;
            _workers = [];

            for (var i = 0; i < minThreads; i++)
            {
                CreateWorker();
            }
        }

        public void QueueTask(Task task)
        {
            if (!_isDisposed)
            {
                _globalQueue.Add(task);
            }
            else
            {
                throw new ObjectDisposedException(nameof(CustomThreadPool));
            }
        }

        private void CreateWorker()
        {
            var worker = new Thread(() =>
            {
                while (!_isDisposed)
                {
                    try
                    {
                        var task = _globalQueue.Take();
                        Console.WriteLine("Thread {0} executing parent high priority task...", Environment.CurrentManagedThreadId);
                        task.RunSynchronously();
                    }
                    catch (InvalidOperationException)
                    {
                        // Ignore if the collection was marked as complete for adding and is empty.
                    }
                }
            })
            {
                IsBackground = true
            };

            _workers.Add(worker);
            worker.Start();
        }

        public static void QueueUserWorkItem(Action action)
        {
            var task = new Task(action);
            GlobalThreadPool.Instance.QueueTask(task);
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _globalQueue.CompleteAdding();

                foreach (var worker in _workers)
                {
                    worker.Join();
                }
            }
        }
    }

    public static class GlobalThreadPool
    {
        public static readonly CustomThreadPool Instance = new CustomThreadPool(4, 10); // Example min/max threads
    }

}
