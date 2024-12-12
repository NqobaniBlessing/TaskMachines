using TaskMachines.TaskSchedulers;

//var singleThreadScheduler = new SingleThreadTaskScheduler();
//var singleThreadSyncContext = singleThreadScheduler.CreateSynchronizationContext();
//SynchronizationContext.SetSynchronizationContext(singleThreadSyncContext);
//var singleThreadFactory = new TaskFactory(singleThreadScheduler);

//for (var i = 0; i < 3; i++)
//{
//    // Queue tasks to single-threaded scheduler
//    await singleThreadFactory.StartNew(() =>
//    {
//        Console.WriteLine("Single-threaded task executed, Thread Id: {0}", Environment.CurrentManagedThreadId);
//    });

//    SynchronizationContext.Current!.Post(
//        _ =>
//        {
//            Console.WriteLine("Task posted to SynchronizationContext executed, Thread Id: {0}",
//                Environment.CurrentManagedThreadId);
//        }, null);
//}

//// Multithreaded scheduler and synchronization context
//var multiThreadScheduler = new DynamicThreadTaskScheduler(minThreads: 10, maxThreads: 20);
//    var multiThreadSyncContext = multiThreadScheduler.CreateSynchronizationContext();
//    SynchronizationContext.SetSynchronizationContext(multiThreadSyncContext);
//    var multiThreadFactory = new TaskFactory(multiThreadScheduler);

//for (var i = 0; i < 10; i++)
//{
//    // Queue tasks to multithreaded scheduler
//    await multiThreadFactory.StartNew(() =>
//    {
//        Console.WriteLine("Parent task");
//        var child = Task.Factory.StartNew(() => {
//            Console.WriteLine("Nested task starting.");
//        });

//        Console.WriteLine(child.CreationOptions);
//    });

//    // Post tasks to SynchronizationContext
//    SynchronizationContext.Current!.Post(_ =>
//    {
//        Console.WriteLine("Task posted to SynchronizationContext executed, Thread Id: {0}", Environment.CurrentManagedThreadId);
//    }, null);
//}

var scheduler = new MultiThreadTaskScheduler();

scheduler.QueueTask(() => Console.WriteLine("Hello from synchronous task, Thread Id: {0}", Environment.CurrentManagedThreadId));

// Queue some asynchronous work
await scheduler.QueueTask(async () =>
{
    await Task.Delay(1000);
    Console.WriteLine("Hello from asynchronous task");
});

// Queue a task that returns a result
var result = await scheduler.QueueTask(async () =>
{
    await Task.Delay(1000);
    return "Result from asynchronous task with result";
});
Console.WriteLine(result);

// Queue a nested task
await scheduler.QueueTask(async () =>
{
    await scheduler.QueueTask(() => Task.Run(() => Console.WriteLine("Hello from nested task")));
    Console.WriteLine("Hello from parent task");
});

scheduler.Shutdown();
