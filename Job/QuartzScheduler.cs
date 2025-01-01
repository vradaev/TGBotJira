using NLog;
using Quartz;
using Quartz.Impl;

namespace JIRAbot.Job;

public class QuartzScheduler
{
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    public static async Task StartScheduler(IServiceProvider serviceProvider)
    {
        try
        {
            Logger.Info("Initializing Quartz scheduler...");

            var schedulerFactory = new StdSchedulerFactory();
            var scheduler = await schedulerFactory.GetScheduler();

            // Используем DI
            scheduler.JobFactory = new JobFactory(serviceProvider);

            // Определяем задачу
            var job = JobBuilder.Create<DashboardJob>()
                .WithIdentity("DashboardJob", "DailyTasks")
                .UsingJobData("chatId", 123456789) // Укажите ваш чат ID
                .UsingJobData("dashboardUrl", "https://your-dashboard-url")
                .UsingJobData("caption", "#supportmetric")
                .UsingJobData("loginUrl", "https://your-login-url")
                .UsingJobData("username", "your-username")
                .UsingJobData("password", "your-password")
                .Build();

            Logger.Info("Dashboard job defined.");

            // Настраиваем выполнение раз в сутки
            var trigger = TriggerBuilder.Create()
                .WithIdentity("DailyTrigger", "FrequentTasks")  // Уникальное имя и группа триггера
                .StartNow()  // Начать сразу
                .WithSchedule(CronScheduleBuilder.CronSchedule("0 1 * * ? *"))  
                .Build();

            Logger.Info("Trigger defined for daily execution at 1:00 AM.");

            // Добавляем задачу и триггер в планировщик
            await scheduler.ScheduleJob(job, trigger);

            Logger.Info("Job scheduled. Starting Quartz scheduler...");
            await scheduler.Start();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to initialize and start Quartz scheduler.");
            throw;
        }
    }
}