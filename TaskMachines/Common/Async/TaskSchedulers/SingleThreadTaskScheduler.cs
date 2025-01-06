using System.Collections.Concurrent;

namespace TaskMachines.TaskSchedulers
{
    public class SingleThreadTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _taskQueue = new();
        private readonly Thread _mainThread;
        private readonly AutoResetEvent _taskAvailable = new(false);

        public SingleThreadTaskScheduler()
        {
            _mainThread = new Thread(ExecuteTasks)
            {
                IsBackground = true
            };
            _mainThread.Start();
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            return [.. _taskQueue];
        }

        protected override void QueueTask(Task task)
        {
            _taskQueue.Enqueue(task);
            _taskAvailable.Set();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            return Thread.CurrentThread == _mainThread && TryExecuteTask(task);
        }

        private void ExecuteTasks()
        {
            while (true)
            {
                _taskAvailable.WaitOne();
                while (_taskQueue.TryDequeue(out var task))
                {
                    TryExecuteTask(task);
                }
            }
        }

        public SynchronizationContext CreateSynchronizationContext()
        {
            return new SingleThreadSynchronizationContext(this, _mainThread);
        }
    }
}
