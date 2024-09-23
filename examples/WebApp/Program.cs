using BackgroundQueue.Core;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddBackgroundJobQueue(builder =>
{
    builder.AddQueueForJob<MyFirstJob>()
           .AddQueueForJob<MySecondJob>(250)
           .FromAssembly<Program>();
});

var app = builder.Build();
app.UseHttpsRedirection();

app.Run();

public class MyFirstJob : Job
{

}

public class MySecondJob : Job
{

}


public class MyFirstJobConsumer(IBackgroundJobQueue jobQueue) : JobQueueProcessor<MyFirstJob>(jobQueue)
{
    public override Task JobExecuteAsync(MyFirstJob job, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

public class MySecondJobConsumer(IBackgroundJobQueue jobQueue) : JobQueueProcessor<MyFirstJob>(jobQueue)
{
    public override Task JobExecuteAsync(MyFirstJob job, CancellationToken cancellationToken)
    {

        return Task.CompletedTask;
    }
}

