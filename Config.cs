using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace JIRAbot;

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } =
        "Host=localhost;Database=TelegramJiraDB;Username=postgres;Password=mysecretpassword";
}

public class JiraConfig
{
    public string Url { get; set; } = "https://your-jira-instance.atlassian.net";
    public string Email { get; set; } = "your-email@example.com";
    public string ApiToken { get; set; } = "your-api-token";
    public string ProjectKey { get; set; } = "PROJECT_KEY";
}

public class TelegramConfig
{
    public string BotToken { get; set; } = "your-telegram-bot-token";
    public string BotUsername { get; set; } = "your_bot_username";
    public List<ChatConfig> Chats { get; set; } = new List<ChatConfig>
    {
        new ChatConfig { ChatId = 1234567890, ClientName = "DefaultClient" }
    };
}

public class ChatConfig
{
    public long ChatId { get; set; }
    public string ClientName { get; set; }
}

public class Config
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public ConnectionStrings ConnectionStrings { get; private set; }
    public JiraConfig Jira { get; private set; }
    public TelegramConfig Telegram { get; private set; }

    public Config(string configFilePath)
    {
        try
        {
            if (!File.Exists(configFilePath))
            {
                Logger.Error($"Configuration file not found: {configFilePath}");
                CreateDefaultConfig(configFilePath);
                Logger.Info("Default configuration file created.");
            }

            Logger.Info("Loading configuration from file: {0}", configFilePath);

            var configJson = File.ReadAllText(configFilePath);
            var config = JObject.Parse(configJson);
            
            ConnectionStrings = config["ConnectionStrings"]?.ToObject<ConnectionStrings>() ??
                                throw new InvalidOperationException("ConnectionStrings configuration section is missing or invalid.");
            Jira = config["Jira"]?.ToObject<JiraConfig>() ??
                   throw new InvalidOperationException("Jira configuration section is missing or invalid.");
            Telegram = config["Telegram"]?.ToObject<TelegramConfig>() ??
                       throw new InvalidOperationException("Telegram configuration section is missing or invalid.");

            Logger.Info("Configuration loaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error loading configuration.");
            throw; // Re-throw exception after logging it
        }
    }

    private void CreateDefaultConfig(string configFilePath)
    {
        var defaultConfig = new
        {
            ConnectionStrings = new ConnectionStrings(),
            Jira = new JiraConfig(),
            Telegram = new TelegramConfig()
        };

        var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
        File.WriteAllText(configFilePath, json);

        Logger.Info("Default configuration file has been created at: {0}", configFilePath);
    }
}