using Microsoft.Extensions.DependencyInjection;
using NLog;
using Quartz;
using Quartz.Spi;

namespace JIRAbot.Job;
public class JobFactory : IJobFactory
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();
    private readonly IServiceProvider _serviceProvider;

    public JobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        Logger.Info($"Creating job instance for: {bundle.JobDetail.Key.Name}");
        // Извлекаем задачу из DI
        var jobType = bundle.JobDetail.JobType;
        return (IJob)_serviceProvider.GetRequiredService(jobType);
    }

    public void ReturnJob(IJob job)
    {
        // В случае необходимости можно здесь реализовать очистку
    }
}