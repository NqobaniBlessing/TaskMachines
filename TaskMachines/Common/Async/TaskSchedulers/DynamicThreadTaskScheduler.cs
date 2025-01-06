using System.Collections.Concurrent;
using TaskMachines.Common.Async.ThreadPools;

namespace TaskMachines.TaskSchedulers
{
    public class DynamicThreadTaskScheduler : TaskScheduler
    {
        // private readonly List<Thread> _threads = [];
        private readonly ConcurrentDictionary<int, ThreadLocal<ConcurrentQueue<Task>>> _threadLocalQueues = new();
        private readonly AutoResetEvent _taskAvailable = new(false);
        private readonly int _minThreads;
        private readonly int _maxThreads;
        private int _activeThreads;
        private readonly object _lockObject = new();

        public DynamicThreadTaskScheduler(int minThreads = 1, int maxThreads = 4)
        {
            this._minThreads = minThreads;
            this._maxThreads = maxThreads;
            this._activeThreads = minThreads;

            for (var i = 0; i < minThreads; i++)
            {
                CreateThread();
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return _threadLocalQueues.Values.SelectMany(tl => tl.Value ?? null).ToArray();
        }

        protected override void QueueTask(Task task)
        {
            if (task.CreationOptions.HasFlag(TaskCreationOptions.DenyChildAttach) || task.CreationOptions.HasFlag(TaskCreationOptions.AttachedToParent))
            {
                GetLocalQueue()?.Enqueue(task);
            }
            else
            {
                CustomThreadPool.QueueUserWorkItem(() => TryExecuteTask(task));
            }
            _taskAvailable.Set();
        }

        private void CreateThread()
        {
            var localQueue = new ThreadLocal<ConcurrentQueue<Task>>(() => new ConcurrentQueue<Task>());
            var threadId = Environment.CurrentManagedThreadId;

            _threadLocalQueues[threadId] = localQueue;

            var thread = new Thread(() => ExecuteTasks(localQueue))
            {
                IsBackground = true
            };

            //_threads.Add(thread);
            thread.Start();
        }

        private void ExecuteTasks(ThreadLocal<ConcurrentQueue<Task>> localQueue)
        {
            while (true)
            {
                _taskAvailable.WaitOne();

                while (localQueue.Value != null && (localQueue.Value.TryDequeue(out var task) || TryStealTask(out task)))
                {
                    if (task != null)
                    {
                        Console.WriteLine("Thread {0} executing child, low priority task...", Environment.CurrentManagedThreadId);
                        TryExecuteTask(task);
                    }
                    AdjustThreadCount();
                }
            }
        }

        private bool TryStealTask(out Task? task)
        {
            foreach (var queue in _threadLocalQueues.Values)
            {
                if (queue.Value == null || !queue.Value.TryDequeue(out task)) continue;
                
                Console.WriteLine("Thread {0} stealing task...", Environment.CurrentManagedThreadId);
                return true;
            }
            task = null;
            return false;
        }

        private void AdjustThreadCount()
        {
            lock (_lockObject)
            {
                var queuedTasks = _threadLocalQueues.Values.Sum(tl => tl.Value?.Count ?? 0);

                if (queuedTasks > _activeThreads && _activeThreads < _maxThreads)
                {
                    CreateThread();
                    _activeThreads++;
                }
                else if (queuedTasks < _activeThreads && _activeThreads > _minThreads)
                {
                    _activeThreads--;
                }
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (Environment.CurrentManagedThreadId != GetThreadId()) return false;

            return taskWasPreviouslyQueued switch
            {
                true when TryDequeue(task) => TryExecuteTask(task),
                false => TryExecuteTask(task),
                _ => false
            };
        }

        protected override bool TryDequeue(Task task)
        {
            foreach (var queue in _threadLocalQueues.Values)
            {
                if (queue.Value == null || !queue.Value.TryDequeue(out var dequeuedTask)) continue;
                
                if (dequeuedTask == task)
                {
                    return true;
                }

                queue.Value.Enqueue(dequeuedTask);
            }

            return false;
        }

        private ConcurrentQueue<Task>? GetLocalQueue()
        {
            var threadId = Environment.CurrentManagedThreadId;

            if (_threadLocalQueues.TryGetValue(threadId, out ThreadLocal<ConcurrentQueue<Task>>? value))
                return value.Value;
            
            var localQueue = new ThreadLocal<ConcurrentQueue<Task>>(() => new ConcurrentQueue<Task>());
            value = localQueue;
            _threadLocalQueues[threadId] = value;

            return value.Value;
        }

        private int GetThreadId()
        {
            return Environment.CurrentManagedThreadId;
        }

        public SynchronizationContext CreateSynchronizationContext()
        {
            return new MultiThreadSynchronizationContext(this);
        }
    }
}
