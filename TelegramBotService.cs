using NLog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JIRAbot;

public class TelegramBotService
{
   private readonly TelegramBotClient _botClient;
    private readonly JiraClient _jiraClient;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly Config _config;
    
    private readonly Dictionary<long, string> _messageToIssueMap = new Dictionary<long, string>();

    // Username of the bot (e.g., "my_bot")
    private readonly string _botUsername;

    private const int PollingInterval = 1000; // 1 second

    public TelegramBotService(string botToken, JiraClient jiraClient, string botUsername, Config config)
    {
        _botClient = new TelegramBotClient(botToken);
        _jiraClient = jiraClient;
        _botUsername = botUsername;
        _config = config ?? throw new ArgumentNullException(nameof(config)); // Инициализация поля конфигурации

    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        Logger.Info("Starting Telegram bot");
        
        var offset = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var updates = await _botClient.GetUpdatesAsync(offset, timeout: 100, limit: 10);

                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    await HandleUpdateAsync(update);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occurred while processing updates.");
            }

            await Task.Delay(PollingInterval, cancellationToken);
        }
    }

    private async Task HandleUpdateAsync(Update update)
    {
        if (update.Message == null) return;
        var message = update.Message;
        
        var (chatConfig, channel) = GetChatConfigAndChannel(message.Chat.Id);
        
        if (chatConfig == null)
        {
            Logger.Info("Ignoring message from chat {0} as it is not configured.", message.Chat.Id);
            return;
        }
        
        // Check if the message contains a mention of the bot
        if (message.Text != null && message.Text.Contains($"@{_botUsername}", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("Received mention of the bot from chat {0}", message.Chat.Id);

            string cleanedText = message.Text.Replace($"@{_botUsername}", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (string.IsNullOrEmpty(cleanedText))
            {
                await _botClient.SendTextMessageAsync(
                    chatId: message.Chat.Id,
                    text: "\u2757 Please provide a description of the issue after mentioning the bot.",
                    parseMode: ParseMode.Html
                );
            }
            else
            {
                Logger.Info("Received mention of the bot from chat {0}", message.Chat.Id);
                string summary = SanitizeSummary(cleanedText);
                string description = cleanedText;
                
                try
                {
                    string issueKey = await _jiraClient.CreateIssueAsync(summary, description, channel);

                    if (!string.IsNullOrEmpty(issueKey))
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"\ud83c\udd95 Issue created: <a href=\"https://ct-ms.atlassian.net/browse/{issueKey}\">{issueKey}</a>", parseMode: ParseMode.Html);
                        _messageToIssueMap[message.MessageId] = issueKey;
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab Failed to create issue.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Error while creating Jira issue.");
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab Error while creating issue.");
                }
                
            }
        }
        else if (message.Photo != null || message.Document != null)
        {
            await HandleMediaMessage(message);
        }
        else if (message.ReplyToMessage != null)
        {
            await HandleReplyMessage(message);
        }
    }

    private async Task HandleMediaMessage(Message message)
    {
        Logger.Info("Received media message from chat {0}", message.Chat.Id);
        
        var (chatConfig, channel) = GetChatConfigAndChannel(message.Chat.Id);
        if (chatConfig == null)
        {
            Logger.Info("Ignoring media message from chat {0} as it is not configured.", message.Chat.Id);
            return;
        }

        var fileId = message.Document?.FileId ?? message.Photo[^1].FileId;
        var fileName = message.Document?.FileName ?? "image.jpg";
        var filePath = await _botClient.GetFileAsync(fileId);

        using (var saveImageStream = new FileStream(fileName, FileMode.Create))
        {
            await _botClient.DownloadFileAsync(filePath.FilePath, saveImageStream);
        }
        
        string cleanedText = message.Caption.Replace($"@{_botUsername}", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (string.IsNullOrEmpty(cleanedText))
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\u2757 Please provide a description of the issue after mentioning the bot.",
                parseMode: ParseMode.Html
            );
        }
        else
        {
            Logger.Info("Received mention of the bot from chat {0}", message.Chat.Id);
            string summary = SanitizeSummary(cleanedText);
            string description = cleanedText;

            try
            {
                string issueKey = await _jiraClient.CreateIssueAsync(summary, description, channel);

                if (!string.IsNullOrEmpty(issueKey))
                {
                    await _jiraClient.AttachFileToIssueAsync(issueKey, fileName);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"Issue created: <a href=\"https://ct-ms.atlassian.net/browse/{issueKey}\">{issueKey}</a>", parseMode: ParseMode.Html);
                    Logger.Info("Issue created in Jira with attachment: {0}", issueKey);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "\\ud83d\\udeab Failed to create issue.");
                    Logger.Error("Failed to create Jira issue with attachment from chat {0}", message.Chat.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error while creating Jira issue with attachment.");
            }
            
        }
    }
    private async Task HandleReplyMessage(Message message)
    {
        if (_messageToIssueMap.TryGetValue(message.ReplyToMessage.MessageId, out var issueKey))
        {
            var commentText = message.Text ?? "No text provided";

            try
            {
                await _jiraClient.AddCommentToIssueAsync(issueKey, commentText);
                await _botClient.SendTextMessageAsync(message.Chat.Id, $"Comment added to issue {issueKey}.");
                Logger.Info("Added comment to Jira issue {0} from chat {1}", issueKey, message.Chat.Id);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to add comment to Jira issue.");
                await _botClient.SendTextMessageAsync(message.Chat.Id, "Failed to add comment to Jira issue.");
            }
        }
        else
        {
            //await _botClient.SendTextMessageAsync(message.Chat.Id, "No corresponding Jira issue found for the reply.");
            Logger.Info("No corresponding Jira issue found for the reply from chat {0}", message.Chat.Id);
        }
    }
    private string SanitizeSummary(string summary)
    {
        // Удаляем символы новой строки и табуляции, заменяем их пробелами
        string sanitized = summary.Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\t", " ");

        // Обрезаем строку до 250 символов, если она превышает этот лимит
        if (sanitized.Length > 250)
        {
            sanitized = sanitized.Substring(0, 250);
        }

        return sanitized;
    }
    private (ChatConfig chatConfig, string channel) GetChatConfigAndChannel(long chatId)
    {
        var chatConfig = _config.Telegram.Chats.FirstOrDefault(c => c.ChatId == chatId);
        var channel = chatConfig?.ClientName;
        return (chatConfig, channel);
    }
}