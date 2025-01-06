
namespace TaskMachines.TaskSchedulers
{
    public class SingleThreadSynchronizationContext(SingleThreadTaskScheduler scheduler, Thread mainThread)
        : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, scheduler);
        }

        public void Dispatch(SendOrPostCallback d, object? state)
        {
            if (Thread.CurrentThread == mainThread)
            {
                d(state);
            }
            else
            {
                Post(d, state);
            }
        }
    }
}