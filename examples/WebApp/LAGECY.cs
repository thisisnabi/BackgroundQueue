//using System.Collections.Generic;
//using System.ComponentModel.DataAnnotations;
//using System.Reflection.Emit;
//using System.Threading.Channels;

//public abstract class BackgroundTaskBase
//{
//    public int Id { get; set; }
//    public bool IsProcessed { get; set; }
//    public DateTime? LockedAt { get; set; } // Timestamp for when the task is locked
//    public string LockedBy { get; set; }    // Pod instance identifier

//    [ConcurrencyCheck] // Enable concurrency control
//    public byte[] RowVersion { get; set; }  // Concurrency token for EF
//}

//public class BackgroundTask<T> : BackgroundTaskBase
//{
//    public T TaskData { get; set; }
//}

//public class AppDbContext : DbContext
//{
//    public DbSet<BackgroundTask<string>> StringTasks { get; set; }
//    public DbSet<BackgroundTask<int>> IntTasks { get; set; }

//    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

//    protected override void OnModelCreating(ModelBuilder modelBuilder)
//    {
//        modelBuilder.Entity<BackgroundTask<string>>()
//            .ToTable("StringTasks")
//            .Property(t => t.RowVersion)
//            .IsRowVersion(); // Configure RowVersion as a concurrency token

//        modelBuilder.Entity<BackgroundTask<int>>()
//            .ToTable("IntTasks")
//            .Property(t => t.RowVersion)
//            .IsRowVersion(); // Same for int tasks
//    }
//}

//public class BackgroundTaskQueue<T>
//{
//    private readonly AppDbContext _dbContext;
//    private readonly string _instanceId; // Unique identifier for the pod instance
//    private readonly Channel<Func<CancellationToken, ValueTask>> _queue;

//    public BackgroundTaskQueue(int capacity, AppDbContext dbContext, string instanceId)
//    {
//        _dbContext = dbContext;
//        _instanceId = instanceId;

//        var options = new BoundedChannelOptions(capacity)
//        {
//            FullMode = BoundedChannelFullMode.Wait
//        };
//        _queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(options);
//    }

//    public async ValueTask QueueBackgroundWorkItemAsync(T taskData, Func<T, CancellationToken, ValueTask> workItem)
//    {
//        if (workItem == null)
//        {
//            throw new ArgumentNullException(nameof(workItem));
//        }

//        var backgroundTask = new BackgroundTask<T>
//        {
//            TaskData = taskData,
//            IsProcessed = false,
//            LockedAt = null,
//            LockedBy = null
//        };

//        _dbContext.Set<BackgroundTask<T>>().Add(backgroundTask);
//        await _dbContext.SaveChangesAsync();

//        await _queue.Writer.WriteAsync(async token => await workItem(taskData, token));
//    }

//    public async ValueTask<Func<CancellationToken, ValueTask>> DequeueAsync(CancellationToken cancellationToken)
//    {
//        Func<CancellationToken, ValueTask> workItem = null;

//        // Retry mechanism for handling concurrency exceptions
//        var retryCount = 3;
//        for (int i = 0; i < retryCount; i++)
//        {
//            try
//            {
//                // Attempt to dequeue and lock the task
//                workItem = await TryDequeueAndLockAsync(cancellationToken);
//                if (workItem != null)
//                {
//                    return workItem;
//                }
//            }
//            catch (DbUpdateConcurrencyException)
//            {
//                // Handle concurrency conflict, retrying if necessary
//                if (i == retryCount - 1) throw; // Throw if max retries are reached
//            }
//        }

//        // If no tasks are available to process, wait for tasks from the in-memory queue
//        return await _queue.Reader.ReadAsync(cancellationToken);
//    }

//    private async Task<Func<CancellationToken, ValueTask>> TryDequeueAndLockAsync(CancellationToken cancellationToken)
//    {
//        using (var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
//        {
//            // Find an unprocessed task that is not locked
//            var task = await _dbContext.Set<BackgroundTask<T>>()
//                .Where(t => !t.IsProcessed && t.LockedAt == null)
//                .OrderBy(t => t.Id)
//                .FirstOrDefaultAsync(cancellationToken);

//            if (task != null)
//            {
//                // Lock the task with concurrency check
//                task.LockedAt = DateTime.UtcNow;
//                task.LockedBy = _instanceId;

//                await _dbContext.SaveChangesAsync(cancellationToken);
//                await transaction.CommitAsync(cancellationToken);

//                // Create the work item delegate
//                Func<CancellationToken, ValueTask> workItem = async token => await Task.CompletedTask; // Replace with actual work
//                await _queue.Writer.WriteAsync(workItem);

//                return workItem;
//            }
//            else
//            {
//                await transaction.RollbackAsync(cancellationToken);
//            }
//        }

//        return null;
//    }

//    public async ValueTask MarkTaskAsCompletedAsync(T taskData, CancellationToken cancellationToken)
//    {
//        var task = await _dbContext.Set<BackgroundTask<T>>()
//            .FirstOrDefaultAsync(t => t.TaskData.Equals(taskData), cancellationToken);

//        if (task != null)
//        {
//            task.IsProcessed = true;
//            task.LockedAt = null;
//            task.LockedBy = null;

//            // Save and handle concurrency issues
//            await _dbContext.SaveChangesAsync(cancellationToken);
//        }
//    }
//}