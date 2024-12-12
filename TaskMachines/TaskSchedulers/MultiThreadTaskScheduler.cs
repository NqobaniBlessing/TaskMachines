using TaskMachines.ThreadPools;

namespace TaskMachines.TaskSchedulers;

public class MultiThreadTaskScheduler : TaskScheduler
{
    public MultiThreadTaskScheduler()
    {
        SynchronizationContext syncContext = new MySynchronizationContext(this);
        SynchronizationContext.SetSynchronizationContext(syncContext);
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return Array.Empty<Task>();
    }

    protected override void QueueTask(Task task)
    {
        if (Task.CurrentId.HasValue)
        {
            MyThreadPool.QueueNestedWorkItem(() =>
            {
                TryExecuteTask(task);
                return Task.CompletedTask;
            });
        }
        else
        {
            MyThreadPool.QueueUserWorkItem(() => TryExecuteTask(task));
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return Thread.CurrentThread.IsThreadPoolThread && TryExecuteTask(task);
    }

    public void QueueTask(Action action)
    {
        MyThreadPool.QueueUserWorkItem(action);
    }

    public Task QueueTask(Func<Task> task)
    {
        return Task.CurrentId.HasValue
            ? MyThreadPool.QueueNestedWorkItem(task)
            : MyThreadPool.QueueUserWorkItem(task);
    }

    public Task<T> QueueTask<T>(Func<Task<T>> task)
    {
        return Task.CurrentId.HasValue
            ? MyThreadPool.QueueNestedWorkItem(task)
            : MyThreadPool.QueueUserWorkItem(task);
    }

    public void Shutdown()
    {
        MyThreadPool.Shutdown();
    }

    private class MySynchronizationContext(MultiThreadTaskScheduler scheduler) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            scheduler.QueueTask(() => d(state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (Thread.CurrentThread.ManagedThreadId == Environment.CurrentManagedThreadId)
            {
                d(state);
            }
            else
            {
                var done = new ManualResetEventSlim();
                Exception? exception = null;
                scheduler.QueueTask(() => Task.Run(() =>
                {
                    try
                    {
                        d(state);
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                }));

                done.Wait();

                if (exception != null) throw exception;
            }
        }
    }
}