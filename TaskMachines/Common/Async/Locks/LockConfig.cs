namespace TaskMachines.Common.Async.Locks;

public class LockConfig
{
    private const int MaximumRetries = 3;
    private const int DefaultRetryDelay = 500; // In milliseconds
    
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxRetries { get; set; } = MaximumRetries;
    public int RetryDelay { get; set; } = DefaultRetryDelay;
}