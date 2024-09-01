using JIRAbot;
using Microsoft.EntityFrameworkCore;
using NLog;

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
            
            using (var context = new AppDbContext(config.ConnectionStrings.DefaultConnection))
            {
                // Автоматическое создание базы данных и таблиц, если их нет
                context.Database.Migrate();
                Logger.Info("Database migration applied successfully.");
            }
            
            var jiraClient = new JiraClient(config.Jira.Url, config.Jira.Email, config.Jira.ApiToken, config.Jira.ProjectKey);
            
            var telegramBotService = new TelegramBotService(config.Telegram.BotToken, jiraClient, config.Telegram.BotUsername, config);
            
            var botTask = telegramBotService.StartAsync(cancellationToken);

            Logger.Info("Bot is running... Press any key to exit.");

            await botTask; 

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
}