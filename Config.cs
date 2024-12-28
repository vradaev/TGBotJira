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
    
    public string CustomField { get; set; } = "labels";
}

public class TelegramConfig
{
    public string BotToken { get; set; } = "your-telegram-bot-token";
    public string BotUsername { get; set; } = "your_bot_username";
    
    public string ChannelAlarm { get; set; } = "idChannel";
}
public class ChatConfig
{
    public long ChatId { get; set; }
    public string ClientName { get; set; }
}
public class NotificationConfig
{
    public SmscConfig Smsc { get; set; } = new SmscConfig();
}
public class SmscConfig
{
    public string Login { get; set; } = "your-smsc-login";
    public string ApiKey { get; set; } = "your-smsc-api-key";
    public string Sender { get; set; } = "your-smsc-sender";
}

public class SuperSetConfig
{
    public string loginUrl { get; set; } = "loginurl";
    public string Login { get; set; } = "login";
    public string Password { get; set; } = "password";
    public string DashboardUrl { get; set; } = "dashurl";
}

public class Config
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public ConnectionStrings ConnectionStrings { get; private set; }
    public JiraConfig Jira { get; private set; }
    public TelegramConfig Telegram { get; private set; }
    
    public List<long> AdminUsers { get; private set; }
    
    public NotificationConfig Notification { get; private set; }
    
    public SuperSetConfig SuperSet { get; private set; }

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
            AdminUsers = config["AdminUsers"]?.ToObject<List<long>>() ??
                         throw new InvalidOperationException("AllowedUsers configuration section is missing or invalid.");
            Notification = config["Notification"]?.ToObject<NotificationConfig>() ??
                           throw new InvalidOperationException("Telegram configuration section is missing or invalid.");
            SuperSet = config["SuperSet"]?.ToObject<SuperSetConfig>() ??
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
            Telegram = new TelegramConfig(),
            AdminUsers = new List<long>(),
            Notification = new NotificationConfig(),
            SuperSet = new SuperSetConfig()
        };

        var json = JsonConvert.SerializeObject(defaultConfig, Formatting.Indented);
        File.WriteAllText(configFilePath, json);

        Logger.Info("Default configuration file has been created at: {0}", configFilePath);
    }
}