using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading.Channels;

namespace BackgroundQueue.Core;

public abstract class Job { }

public interface IBackgroundJobQueue
{
    Task QueueAsync<T>(T Item, CancellationToken cancellationToken = default) where T : Job;

    Task<T> DequeueAsync<T>(CancellationToken cancellationToken = default) where T : Job;
}

public class BackgroundJobQueue : IBackgroundJobQueue
{

    private readonly Dictionary<Type, Channel<Job>> _queues;

    public BackgroundJobQueue(Dictionary<Type, Channel<Job>> queues)
    {
        _queues = queues;
    }

    public async Task QueueAsync<T>(T item, CancellationToken cancellationToken = default) where T : Job
    {
        if (_queues.TryGetValue(typeof(T), out var channel))
        {
            await channel.Writer.WriteAsync(item, cancellationToken);
        }
        throw new InvalidOperationException("Queue not found for type " + typeof(T).Name);
    }

    public async Task<T> DequeueAsync<T>(CancellationToken cancellationToken = default) where T : Job
    {
        if (_queues.TryGetValue(typeof(T), out var channel))
        {
            var job = await channel.Reader.ReadAsync(cancellationToken);
            return (T)job;
        }
        throw new InvalidOperationException("Queue not found for type " + typeof(T).Name);
    }

}

public class BackgroundJobQueueBuilder
{
    private readonly Dictionary<Type, Channel<Job>> _queues = new Dictionary<Type, Channel<Job>>();
    public Assembly Assembly { get; private set; } = null!;

    public BackgroundJobQueueBuilder AddQueueForJob<T>(int capacity = 500) where T : Job
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            Capacity = capacity
        };

        var channel = Channel.CreateBounded<Job>(options);
        _queues[typeof(T)] = channel;
        return this;
    }

    public BackgroundJobQueueBuilder FromAssembly<T>() where T : class
    {
        Assembly = typeof(T).Assembly;
        return this;
    }

    public BackgroundJobQueue Build()
    {
        return new BackgroundJobQueue(_queues);
    }
}

public static class BackgroundJobQueueExtensions
{
    public static IServiceCollection AddBackgroundJobQueue(this IServiceCollection services, Action<BackgroundJobQueueBuilder> configure)
    {
        var builder = new BackgroundJobQueueBuilder();
        configure(builder);

  
        var queue = builder.Build();
        services.AddSingleton<IBackgroundJobQueue>(queue);
        var processorTypes = builder.Assembly.GetTypes()
                                             .Where(t => !t.IsAbstract && !t.IsInterface)
                                             .Where(t => IsSubclassOfRawGeneric(typeof(JobQueueProcessor<>), t))
                                             .ToList();
       
        foreach (var processorType in processorTypes)
        {
             
            var addHostedServiceMethod = typeof(ServiceCollectionHostedServiceExtensions)
                                                .GetMethods().FirstOrDefault(d => d.Name == "AddHostedService" && d.IsPublic && d.IsStatic)
                                                ?.MakeGenericMethod(processorType);

            addHostedServiceMethod?.Invoke(null, new object[] { services });
        }

        return services;
    }

    private static bool IsSubclassOfRawGeneric(Type generic, Type toCheck)
    {
        while (toCheck != null && toCheck != typeof(object))
        {
            var cur = toCheck.IsGenericType ? toCheck.GetGenericTypeDefinition() : toCheck;
            if (generic == cur)
            {
                return true;
            }
            toCheck = toCheck.BaseType;
        }
        return false;
    }

}

 

public abstract class JobQueueProcessor<TJob> : BackgroundService where TJob : Job
{
    private readonly IBackgroundJobQueue _jobQueue;
  
    public JobQueueProcessor(IBackgroundJobQueue jobQueue)
    {
        _jobQueue = jobQueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
       
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var job = await _jobQueue.DequeueAsync<TJob>();
                await JobExecuteAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing job of type {nameof(TJob)}: {ex.Message}");
            }
        }
    }

    public abstract Task JobExecuteAsync(TJob job, CancellationToken cancellationToken);
}


public class BackgroundJob
{
    public int Id { get; set; }
    public required string Data { get; set; }
    public required string DataType { get; set; }
    public bool IsProcessed { get; set; }

    [ConcurrencyCheck]
    public DateTimeOffset LockedOn { get; set; }
}