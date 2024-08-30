using JIRAbot;
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
            
            var jiraClient = new JiraClient(config.Jira.Url, config.Jira.Email, config.Jira.ApiToken, config.Jira.ProjectKey);
            
            var telegramBotService = new TelegramBotService(config.Telegram.BotToken, jiraClient, config.Telegram.BotUsername, config);
            
            var botTask = telegramBotService.StartAsync(cancellationToken);

            Logger.Info("Bot is running... Press any key to exit.");
           // Console.ReadKey();
            
         //   cancellationTokenSource.Cancel();
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