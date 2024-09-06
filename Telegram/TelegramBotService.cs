using Microsoft.EntityFrameworkCore;
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
    private readonly MediaHandlerService _mediaHandlerService;
    private readonly AppDbContext _context;
    
    private readonly Dictionary<long, string> _messageToIssueMap = new Dictionary<long, string>();
    
    private readonly string _botUsername;

    private const int PollingInterval = 1000; // 1 second

    public TelegramBotService(string botToken, JiraClient jiraClient, string botUsername, Config config, MediaHandlerService mediaHandlerService, AppDbContext context)
    {
        _botClient = new TelegramBotClient(botToken);
        _jiraClient = jiraClient;
        _botUsername = botUsername;
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _mediaHandlerService = mediaHandlerService ?? throw new ArgumentNullException(nameof(mediaHandlerService));
        _context = context ?? throw new ArgumentNullException(nameof(context));

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
        
        if (message.Text.StartsWith("/addclient"))
        {
            await HandleAddClientCommand(message);
            return;
        }
        
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
                        Logger.Info("Issue created in Jira: {0}", issueKey);
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab Failed to create issue.");
                        Logger.Error("Failed to create Jira issue from chat {0}", message.Chat.Id);
                        
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
        await _mediaHandlerService.HandleMediaMessageAsync(message, _botUsername, _messageToIssueMap, GetChatConfigAndChannel);
    }

    private async Task HandleReplyMessage(Message message)
    {
        string issueKey = null;
        Message currentMessage = message;
        
        if (_messageToIssueMap.TryGetValue(message.ReplyToMessage.MessageId, out issueKey))
        {
            var commentText = message.Text ?? "No text provided";

            try
            {
                await _jiraClient.AddCommentToIssueAsync(issueKey, commentText);
                
                if (message.Photo != null || message.Document != null)
                {
                    await AttachFilesToIssueAsync(message, issueKey);
                }

                // Сохраняем идентификатор нового комментария и связываем его с задачей
                _messageToIssueMap[message.MessageId] = issueKey;

                await _botClient.SendTextMessageAsync(message.Chat.Id, $"\ud83d\udcdd Comment added: <a href=\"https://ct-ms.atlassian.net/browse/{issueKey}\">{issueKey}</a>", parseMode: ParseMode.Html);
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
            Logger.Info("No corresponding Jira issue found for the reply from chat {0}", message.Chat.Id);
        }
    }
    private string SanitizeSummary(string summary)
    {
        string sanitized = summary.Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\t", " ");
        
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

    private async Task AttachFilesToIssueAsync(Message message, string issueKey)
    {
        try
        {
            string fileId = message.Document?.FileId ?? message.Photo?.LastOrDefault()?.FileId;
            string fileName = message.Document?.FileName ?? "image.jpg";

            var filePath = await _botClient.GetFileAsync(fileId);

            if (filePath == null)
            {
                Logger.Error("Failed to retrieve file path for fileId {0} from chat {1}.", fileId, message.Chat.Id);
                await _botClient.SendTextMessageAsync(message.Chat.Id, "Failed to retrieve the file.");
                return;
            }

            using (var saveImageStream = new FileStream(fileName, FileMode.Create))
            {
                await _botClient.DownloadFileAsync(filePath.FilePath, saveImageStream);
            }

            await _jiraClient.AttachFileToIssueAsync(issueKey, fileName);
            _messageToIssueMap[message.MessageId] = issueKey;

            Logger.Info("Attached file to Jira issue {0}: {1}", issueKey, fileName);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to attach file to Jira issue {0}.", issueKey);
            await _botClient.SendTextMessageAsync(message.Chat.Id, "Failed to attach file to Jira issue.");
        }
    }
private async Task HandleAddClientCommand(Message message)
{
    var commandParts = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (commandParts.Length < 2)
    {
        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Usage: /addclient client_name",
            parseMode: ParseMode.Html
        );
        return;
    }

    string clientName = commandParts[1];

    try
    {
        // Проверка, связана ли эта группа уже с каким-либо клиентом
        var existingGroup = await _context.Groups
            .FirstOrDefaultAsync(g => g.GroupId == message.Chat.Id.ToString());

        if (existingGroup != null)
        {
            // Группа уже связана с клиентом, ошибка
            var associatedClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == existingGroup.СlientId);

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"This chat is already associated with client '{associatedClient?.Name}'.",
                parseMode: ParseMode.Html
            );

            Logger.Info($"Chat '{existingGroup.Name}' is already associated with client '{associatedClient?.Name}'.");
            return;
        }

        // Проверка, существует ли уже клиент с таким именем
        var existingClient = await _context.Clients
            .FirstOrDefaultAsync(c => c.Name == clientName);

        if (existingClient != null)
        {
            // Клиент существует, создаем новую группу для этого клиента
            var newGroup = new Groups
            {
                GroupId = message.Chat.Id.ToString(),
                Name = (await _botClient.GetChatAsync(message.Chat.Id)).Title,
                СlientId = existingClient.Id, // Привязка группы к клиенту
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Client '{clientName}' exists. Chat '{newGroup.Name}' has been successfully added to this client.",
                parseMode: ParseMode.Html
            );

            Logger.Info($"Client '{clientName}' exists. Chat '{newGroup.Name}' added successfully.");
        }
        else
        {
            // Если клиент не существует, создаем нового клиента и группу
            var channel = await _botClient.GetChatAsync(message.Chat.Id);
            var newClient = new Clients
            {
                Name = clientName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Clients.Add(newClient);
            await _context.SaveChangesAsync();

            var newGroup = new Groups
            {
                GroupId = message.Chat.Id.ToString(),
                Name = channel.Title,
                СlientId = newClient.Id, // Привязка группы к новому клиенту
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"Client '{clientName}' and chat '{channel.Title}' have been successfully added.",
                parseMode: ParseMode.Html
            );

            Logger.Info($"Client '{clientName}' and chat '{channel.Title}' added successfully.");
        }
    }
    catch (Exception ex)
    {
        Logger.Error(ex, "An error occurred while processing /addclient command.");
        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "An error occurred while processing your request. Please ensure the channel name is correct and try again.",
            parseMode: ParseMode.Html
        );
    }
}
}