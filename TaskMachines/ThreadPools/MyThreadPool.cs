﻿using System.Collections.Concurrent;
using System.Diagnostics;

namespace TaskMachines.ThreadPools;

public static class MyThreadPool
{
    private static readonly BlockingCollection<(Func<Task>, ExecutionContext?)> GlobalQueue = [];
    private static readonly CancellationTokenSource CancellationTokenSource = new();
    private static readonly CancellationToken CancellationToken = CancellationTokenSource.Token;
    private static readonly ThreadLocal<ConcurrentStack<(Func<Task>, ExecutionContext?)>> LocalQueues =
        new(() => new ConcurrentStack<(Func<Task>, ExecutionContext?)>());
    private static readonly CountdownEvent Countdown = new(1);  // Start with 1 to prevent early completion

    static MyThreadPool()
    {
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            var thread = new Thread(Start)
            {
                IsBackground = true
            };

            thread.UnsafeStart();
        }
    }

    private static async void Start()
    {
        try
        {
            while (!CancellationToken.IsCancellationRequested)
            {
                if (LocalQueues.Value != null && LocalQueues.Value.TryPop(out var localWorkItem))
                {
                    await ExecuteTask(localWorkItem);
                }
                else if (GlobalQueue.TryTake(out var globalWorkItem, Timeout.Infinite, CancellationToken))
                {
                    await ExecuteTask(globalWorkItem);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine($"Bye bye from {nameof(MyThreadPool)}");
        }
        finally
        {
            if (Countdown.CurrentCount != 0) Countdown.Signal();
        }
    }

    public static void QueueUserWorkItem(Action workItem)
    {
        QueueUserWorkItem(() =>
        {
            workItem();
            return Task.CompletedTask;
        });
    }

    public static Task QueueUserWorkItem(Func<Task> workItem)
    {
        var context = ExecutionContext.Capture();
        Countdown.AddCount();
        GlobalQueue.Add((async () =>
        {
            try
            {
                await workItem();
            }
            finally
            {
                Countdown.Signal();
            }
        }, context));

        return Task.CompletedTask;
    }

    public static Task<T> QueueUserWorkItem<T>(Func<Task<T>> workItem)
    {
        var tcs = new TaskCompletionSource<T>();
        QueueUserWorkItem(async () =>
        {
            try
            {
                var result = await workItem();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static async Task ExecuteTask((Func<Task> task, ExecutionContext? context) workItem)
    {
        var (task, context) = workItem;
        if (context is null)
        {
            await task();
        }
        else
        {
            await Task.Factory.StartNew(async () =>
            {
                await task();
            }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();
        }
    }

    public static Task QueueNestedWorkItem(Func<Task> workItem)
    {
        var context = ExecutionContext.Capture();
        Countdown.AddCount();
        LocalQueues.Value?.Push((async () =>
        {
            try
            {
                await workItem();
            }
            finally
            {
                Countdown.Signal();
            }
        }, context));

        return Task.CompletedTask;
    }

    public static Task<T> QueueNestedWorkItem<T>(Func<Task<T>> workItem)
    {
        var tcs = new TaskCompletionSource<T>();
        QueueNestedWorkItem(async () =>
        {
            try
            {
                var result = await workItem();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    public static void Shutdown()
    {
        GlobalQueue.CompleteAdding();
        Countdown.Signal();
        Countdown.Wait();
        CancellationTokenSource.Cancel();
    }
}