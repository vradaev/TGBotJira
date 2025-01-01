using System.Threading.Channels;
using JIRAbot;
using JIRAbot.Alarm;
using JIRAbot.Job;
using JIRAbot.SuperSet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Quartz.Spi;
using Telegram.Bot;

class Program
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    static async Task Main(string[] args)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        try
        {
            Logger.Info("Application started");
            
            var config = new Config("config.json");
            var channelId = config.Telegram.ChannelAlarm;
            
            var serviceProvider = ConfigureServices(config);
            await QuartzScheduler.StartScheduler(serviceProvider);

            
            var dbContextOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(config.ConnectionStrings.DefaultConnection)
                .Options;

            using (var context = new AppDbContext(config.ConnectionStrings.DefaultConnection))
            {
                // Автоматическое создание базы данных и таблиц, если их нет
                context.Database.Migrate();
                Logger.Info("Database migration applied successfully.");
            

            var jiraClient = new JiraClient(config.Jira.Url, config.Jira.Email, config.Jira.ApiToken, config.Jira.ProjectKey, config.Jira.CustomField);
            
            var botClient = new TelegramBotClient(config.Telegram.BotToken);
            
            var mediaHandlerService = new MediaHandlerService(botClient, jiraClient);
            
            var chatConfigService = new ChatConfigService(context);
            var notificationService = new NotificationService(config.Notification.Smsc, context);
            var sendDashboardService = new SendDashboardService(botClient);
            var telegramBotService = new TelegramBotService(config.Telegram.BotToken, jiraClient, config.Telegram.BotUsername, mediaHandlerService, context, chatConfigService, config, channelId, notificationService, sendDashboardService);
            var jiraTicketService = new JiraTicketService(context, jiraClient);
            
            var syncTask = StartJiraSyncPeriodically(jiraTicketService, cancellationToken);
            var botTask = telegramBotService.StartAsync(cancellationToken);
            
            

            Logger.Info("Bot is running... Press any key to exit.");

            await Task.WhenAny(botTask, syncTask); 
            
            }
            Logger.Info("Application stopped");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "An error occurred");
        }
        finally
        {
            LogManager.Shutdown(); 
        }
    }
    
    private static IServiceProvider ConfigureServices(Config config)
    {
        var serviceCollection = new ServiceCollection();

        // Добавляем конфигурацию и зависимости в DI
        serviceCollection.AddSingleton(new TelegramBotClient(config.Telegram.BotToken));
        serviceCollection.AddSingleton(config);
        serviceCollection.AddTransient<SendDashboardService>();
        serviceCollection.AddTransient<DashboardJob>(); // Задача для Quartz
        serviceCollection.AddSingleton<IJobFactory, JobFactory>();

        // Настраиваем другие сервисы
        // Например, TelegramBotClient, JiraClient и т.д.

        return serviceCollection.BuildServiceProvider();
    }
    
    private static async Task StartJiraSyncPeriodically(JiraTicketService jiraTicketService, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Logger.Info("Starting Jira ticket sync...");
                await jiraTicketService.SaveJiraTicketsAsync(); // Синхронизируем тикеты
                Logger.Info("Jira ticket sync completed.");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error during Jira ticket sync");
            }

            await Task.Delay(TimeSpan.FromHours(1), cancellationToken); // Пауза между синхронизациями (например, 1 час)
        }
    }
}