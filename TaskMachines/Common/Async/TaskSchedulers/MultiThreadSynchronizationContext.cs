namespace TaskMachines.TaskSchedulers
{
    public class MultiThreadSynchronizationContext(DynamicThreadTaskScheduler scheduler) : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object? state)
        {
            Task.Factory.StartNew(() => d(state), CancellationToken.None, TaskCreationOptions.None, scheduler);
        }
    }
}
