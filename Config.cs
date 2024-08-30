using Newtonsoft.Json.Linq;
using NLog;

namespace JIRAbot;

public class JiraConfig
{
    public string Url { get; set; }
    public string Email { get; set; }
    public string ApiToken { get; set; }
    public string ProjectKey { get; set; }
}

public class TelegramConfig
{
    public string BotToken { get; set; }
    public string BotUsername { get; set; }
    public List<ChatConfig> Chats { get; set; }
}

public class ChatConfig
{
    public long ChatId { get; set; }
    public string ClientName { get; set; }
}

public class Config
{
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public JiraConfig Jira { get; private set; }
        public TelegramConfig Telegram { get; private set; }

        public Config(string configFilePath)
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    Logger.Error($"Configuration file not found: {configFilePath}");
                    throw new FileNotFoundException("Configuration file not found.", configFilePath);
                }

                Logger.Info("Loading configuration from file: {0}", configFilePath);

                var configJson = File.ReadAllText(configFilePath);
                var config = JObject.Parse(configJson);

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
    }