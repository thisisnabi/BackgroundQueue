using BackgroundQueue.Core;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBackgroundJobQueue(builder =>
{
    builder.AddQueueForJob<MyFirstJob>()
           .AddQueueForJob<MySecondJob>(250)
           .ScanProcessorFromAssembly<Program>();
});

var app = builder.Build();
app.UseHttpsRedirection();

app.MapGet("/a", (IBackgroundJobQueue c) => { 
    c.QueueAsync(new  MyFirstJob());
});
app.MapGet("/b", (IBackgroundJobQueue c) => {
    c.QueueAsync(new MySecondJob());
});

app.Run();

public class MyFirstJob : Job
{
    // 

}

public class MySecondJob : Job
{

}


public class MyFirstJobConsumer(IBackgroundJobQueue jobQueue) 
    : JobQueueProcessor<MyFirstJob>(jobQueue)
{
    public override Task JobExecuteAsync(MyFirstJob job, CancellationToken cancellationToken)
    {
        // update read model!
        return Task.CompletedTask;
    }
}

public class MySecondJobConsumer(IBackgroundJobQueue jobQueue) : JobQueueProcessor<MySecondJob>(jobQueue)
{
    public override Task JobExecuteAsync(MySecondJob job, CancellationToken cancellationToken)
    {
        // update read model!
        return Task.CompletedTask;
    }
}




public class TestDbContext : DbContext
{

}