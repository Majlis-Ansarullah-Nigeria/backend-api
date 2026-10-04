namespace HRS.Worker;

public partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            LogRunningAt(DateTimeOffset.Now);
            await Task.Delay(1000, stoppingToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker running at: {RunTime}")]
    private partial void LogRunningAt(DateTimeOffset runTime);
}
