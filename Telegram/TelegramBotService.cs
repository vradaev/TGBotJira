using System.Net;
using System.Net.Http.Json;
using JIRAbot.Alarm;
using Microsoft.EntityFrameworkCore;
using NLog;
using PuppeteerSharp;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;


namespace JIRAbot;

public class TelegramBotService
{
    private readonly TelegramBotClient _botClient;
    private readonly JiraClient _jiraClient;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IChatConfigService _chatConfigService;
    private readonly MediaHandlerService _mediaHandlerService;
    private readonly AppDbContext _context;
    private readonly Config _config;
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly EscalationService _escalationService;
    
    private readonly Dictionary<long, string> _messageToIssueMap = new Dictionary<long, string>();
    
    private readonly string _botUsername;

    private const int PollingInterval = 1000; // 1 second
    
    private long _sosChatId;
    private int _sosMessageId;

    public TelegramBotService(string botToken, JiraClient jiraClient, string botUsername, MediaHandlerService mediaHandlerService, AppDbContext context, IChatConfigService chatConfigService, Config config, string channelId, NotificationService notificationService)
    {
        _botClient = new TelegramBotClient(botToken);
        _jiraClient = jiraClient;
        _botUsername = botUsername;
        _mediaHandlerService = mediaHandlerService ?? throw new ArgumentNullException(nameof(mediaHandlerService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _chatConfigService = chatConfigService ?? throw new ArgumentNullException(nameof(chatConfigService));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _escalationService = new EscalationService(_botClient, channelId, notificationService);

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
        if (update.CallbackQuery != null)
        {
            await HandleCallbackQueryAsync(update.CallbackQuery);
        }

        var message = update.Message;
        
        if (message == null)
        {
            Logger.Debug("Update does not contain a message.");
            return;
        }
        
        if (message.Text != null)
        {
            if (await ProcessCommandAsync(message))
            {
                return;
            }
            
            var (chatConfig, channel) = await GetChatConfigAndChannelAsync(message.Chat.Id);
            if (chatConfig == null)
            {
                Logger.Info("Ignoring message from chat {0} as it is not configured.", message.Chat.Id);
                return;
            }
            
            if (message.Text.Contains($"@{_botUsername}", StringComparison.OrdinalIgnoreCase))
            {
                Logger.Info("Received mention of the bot from chat {0}", message.Chat.Id);

                string cleanedText = message.Text.Replace($"@{_botUsername}", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();
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
                            var sentMessage = await _botClient.SendTextMessageAsync(message.Chat.Id,
                                $"\ud83c\udd95 Issue created: <a href=\"https://ct-ms.atlassian.net/browse/{issueKey}\">{issueKey}</a>",
                                parseMode: ParseMode.Html);
                            _messageToIssueMap[message.MessageId] = issueKey;
                            _messageToIssueMap[sentMessage.MessageId] = issueKey;
                            Logger.Info("Issue created in Jira: {0}", issueKey);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id,
                                "\ud83d\udeab Failed to create issue.");
                            Logger.Error("Failed to create Jira issue from chat {0}", message.Chat.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error while creating Jira issue.");
                        await _botClient.SendTextMessageAsync(message.Chat.Id,
                            "\ud83d\udeab Error while creating issue.");
                    }
                }
            }
            else if (message.ReplyToMessage != null)
            {
                await HandleReplyMessage(message);
            }
        }
        else if (message.Photo != null || message.Document != null)
        {
            // Сообщение содержит медиа
            await HandleMediaMessage(message);
        }
    }

    private async Task HandleMediaMessage(Message message)
    {
        var (chatConfig, channel) = await _chatConfigService.GetChatConfigAndChannelAsync(message.Chat.Id);
        await _mediaHandlerService.HandleMediaMessageAsync(message, _botUsername, _messageToIssueMap, chatConfig);
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

               // await _botClient.SendTextMessageAsync(message.Chat.Id, $"\ud83d\udcdd Comment added: <a href=\"https://ct-ms.atlassian.net/browse/{issueKey}\">{issueKey}</a>", parseMode: ParseMode.Html);
                Logger.Info("Added comment to Jira issue {0} from chat {1}", issueKey, message.Chat.Id);
                
                await AddReactionAsync(message.Chat.Id, message.MessageId, "\u270d");
                
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
    private async Task<(ChatConfig chatConfig, string channel)> GetChatConfigAndChannelAsync(long chatId)
    {
        var group = await _context.Groups
            .Include(g => g.Client)
            .FirstOrDefaultAsync(g => g.GroupId == chatId.ToString());

        if (group != null)
        {
            var chatConfig = new ChatConfig
            {
                ChatId = chatId,
                ClientName = group.Client?.Name
            };

            var channel = group.Client?.Name;
            return (chatConfig, channel);
        }

        return (null, null);
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
            text: "\u26a0\ufe0f Usage command: /addclient client_name",
            parseMode: ParseMode.Html
        );
        return;
    }

    string clientName = commandParts[1];

    try
    {
        var existingGroup = await _context.Groups
            .FirstOrDefaultAsync(g => g.GroupId == message.Chat.Id.ToString());

        if (existingGroup != null)
        {
            var associatedClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == existingGroup.ClientId);

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"\ud83e\udeaa This chat is already associated with client '{associatedClient?.Name}'.",
                parseMode: ParseMode.Html
            );

            Logger.Info($"Chat '{existingGroup.Name}' is already associated with client '{associatedClient?.Name}'.");
            return;
        }
        
        var existingClient = await _context.Clients
            .FirstOrDefaultAsync(c => c.Name == clientName);

        if (existingClient != null)
        {
            var newGroup = new Group
            {
                GroupId = message.Chat.Id.ToString(),
                Name = (await _botClient.GetChatAsync(message.Chat.Id)).Title,
                ClientId = existingClient.Id, 
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"\u2755 Client '{clientName}' exists. Chat '{newGroup.Name}' has been successfully added to this client.",
                parseMode: ParseMode.Html
            );

            Logger.Info($"Client '{clientName}' exists. Chat '{newGroup.Name}' added successfully.");
        }
        else
        {
            var channel = await _botClient.GetChatAsync(message.Chat.Id);
            var newClient = new Client
            {
                Name = clientName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Clients.Add(newClient);
            await _context.SaveChangesAsync();

            var newGroup = new Group
            {
                GroupId = message.Chat.Id.ToString(),
                Name = channel.Title,
                ClientId = newClient.Id, 
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"\ud83e\udee1 Client '{clientName}' and chat '{channel.Title}' have been successfully added.",
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
private async Task<bool> ProcessCommandAsync(Message message)
{
    if (message.Text.StartsWith("/addclient"))
    {
        // Проверка, является ли пользователь администратором
        if (!_config.AdminUsers.Contains((int)message.From.Id))
        {
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\ud83d\uded1 You are not authorized to use this command.",
                parseMode: ParseMode.Html
            );
            Logger.Info($"User '{message.From.Id}' is not authorized to use the /addclient command in the channel {message.Chat.Id}.");
            return false;
        }

        // Обработка команды /addclient
        await HandleAddClientCommand(message);
        return true;
    }

    if (message.Text.StartsWith("/dashboard"))
    {
        var dashboardUrl = _config.SuperSet.DashboardUrl; 
        var caption = "Скриншот дашборда Superset";
        var loginUrl = _config.SuperSet.loginUrl;
        var username = _config.SuperSet.Login;
        var password = _config.SuperSet.Password;
        
        await SendDashboardScreenshotAsync(message.Chat.Id, dashboardUrl, caption, loginUrl, username, password);
        return true;
    }

    if (message.Text.StartsWith("/sos"))
    {
        _sosChatId = message.Chat.Id;
        _sosMessageId = message.MessageId;
        
        var channelName = message.Chat.Title ?? "Неизвестный канал"; 
        var alertId = UniqueIdGenerator.GenerateUniqueId();
        
        Logger.Info("Generate new alert id: {0}", alertId);

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new []
            {
                InlineKeyboardButton.WithCallbackData("Принять тревогу", $"accept_alert|{channelName}|{alertId}")
            }
        });
        
        await _escalationService.SendAlertAsync($"🚨 Тревога из группы: {channelName}!", inlineKeyboard, message.Chat.Id, _sosMessageId, channelName, alertId);
    
        return true;
    }
    
    if (message.Text.StartsWith("/cachereload"))
    {
        await ReloadCacheAsync(message);
        return true;
    }

    return false;
}
private async Task AddReactionAsync(long chatId, int messageId, string emoji, bool isBig = true)
{
    var botToken = _config.Telegram.BotToken;
    var url = $"https://api.telegram.org/bot{botToken}/setMessageReaction";

    var payload = new
    {
        chat_id = chatId,
        message_id = messageId,
        reaction = new[]
        {
            new { type = "emoji", emoji = emoji }
        },
        is_big = isBig
    };

    var response = await _httpClient.PostAsJsonAsync(url, payload);

    if (response.IsSuccessStatusCode)
    {
        Logger.Info("Reaction added successfully.");
    }
    else
    {
        var errorMessage = await response.Content.ReadAsStringAsync();
        Logger.Error($"Failed to add reaction. Response: {errorMessage}");
    }
}

private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
{
    if (callbackQuery.Data.StartsWith("accept_alert"))
    {
        var dataParts = callbackQuery.Data.Split('|');
        var channelName = dataParts.Length > 1 ? dataParts[1] : "Неизвестный канал";
        var alertId = dataParts[2];
        
        var userName = $"@{callbackQuery.From.Username}";
        
        
        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Тревога принята");
        
        await _escalationService.HandleAcceptSos(_sosChatId, _sosMessageId, callbackQuery.Message.MessageId, callbackQuery.Message.Chat.Id, userName, channelName, alertId);
        

        
    }
}

private async Task ReloadCacheAsync(Message message) // Temporary workaround for hello
{
    try
    {
        var keyName = "cachereloadhello";
        string? value = null;

        try
        {
            // Попытка получить значение из базы данных
            value = await _context.Settings
                .Where(s => s.KeyName == keyName)
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
        }
        catch (Exception dbEx)
        {
            Logger.Warn($"Error accessing database: {dbEx.Message}");
        }

        if (string.IsNullOrEmpty(value))
        {
            // Сообщаем, если значение в БД не найдено
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "⚠️ URL for cache reload not found in settings.",
                parseMode: ParseMode.Html
            );
            Logger.Warn("URL for cache reload not found in settings.");
            return;
        }

        using var client = new HttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, value);

        try
        {
            // Выполняем запрос
            var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: "\ud83e\udee1 Order cache reloaded successfully.",
                parseMode: ParseMode.Html
            );
            Logger.Info($"Cache reloaded successfully: {result}");
        }
        catch (Exception httpEx)
        {
            // Обработка ошибок HTTP-запроса
            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id,
                text: $"❌ Error reloading order cache: {httpEx.Message}",
                parseMode: ParseMode.Html
            );
            Logger.Error($"Error reloading cache: {httpEx.Message}");
        }
    }
    catch (Exception ex)
    {
        // Общая обработка ошибок
        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: $"❌ Unexpected error: {ex.Message}",
            parseMode: ParseMode.Html
        );
        Logger.Error($"Unexpected error: {ex.Message}");
    }
}

public class UniqueIdGenerator
{
    private static int currentId = 0;
    private const int maxId = 9999;

    public static string GenerateUniqueId()
    {
        if (currentId >= maxId)
        {
            throw new InvalidOperationException("Достигнуто максимальное количество уникальных идентификаторов.");
        }
        currentId++;
        return currentId.ToString();;
    }
}

  public async Task SendDashboardScreenshotAsync(long chatId, string dashboardUrl, string caption, string loginUrl, string username, string password)
    {
        // Логируем начало процесса
        Logger.Info($"Starting to capture and send screenshot for dashboard: {dashboardUrl}");

        var screenshotPath = Path.Combine(Path.GetTempPath(), "dashboard_screenshot.png");

        try
        {
            // Захватываем скриншот
            Logger.Info("Capturing screenshot...");
            await CaptureScreenshotAsync(dashboardUrl, screenshotPath, loginUrl, username, password);

            // Отправляем скриншот в Telegram
            Logger.Info("Sending screenshot to Telegram...");
            using (var stream = new FileStream(screenshotPath, FileMode.Open))
            {
                var fileToSend = new InputFileStream(stream, "dashboard_screenshot.png");
                await _botClient.SendPhotoAsync(chatId, fileToSend);
            }

            // Удаляем временный файл
            Logger.Info("Deleting temporary screenshot file...");
            System.IO.File.Delete(screenshotPath);

            Logger.Info("Screenshot sent and temporary file deleted successfully.");
        }
        catch (Exception ex)
        {
            // Логируем ошибку, если что-то пошло не так
            Logger.Error(ex, "An error occurred while sending the dashboard screenshot.");
        }
    }

    private async Task CaptureScreenshotAsync(string dashboardUrl, string filePath, string loginUrl, string username, string password)
    {
        try
        {
            Logger.Info("Downloading Puppeteer browser...");
            var browserFetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Path = "/app/.chrome" 
            });
            await browserFetcher.DownloadAsync();
            
            // Запуск браузера с указанием пути к Chromium
            var browserOptions = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = "/app/.chrome/Chrome/Linux-130.0.6723.69/chrome-linux64/chrome"
            };
            
            Logger.Info("Launching browser...");
            using var browser = await Puppeteer.LaunchAsync(browserOptions);
            using var page = await browser.NewPageAsync();
            
            Logger.Info("Navigating to login page...");
            await page.GoToAsync(loginUrl);
            
            Logger.Info($"Entering credentials for user: {username}");
            await page.TypeAsync("input[name='username']", username);
            await page.TypeAsync("input[name='password']", password);
            
            await page.WaitForSelectorAsync("input.btn.btn-primary[type='submit'][value='Sign In']", new WaitForSelectorOptions { Timeout = 5000 });

            // Click to the button
            await page.ClickAsync("input.btn.btn-primary[type='submit'][value='Sign In']");

            // Waiting page after login
            Logger.Info("Waiting for navigation after login...");
            await page.WaitForNavigationAsync();
            
            // set screen size
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1280,    // Ширина
                Height = 1280,   // Высота
                IsMobile = false, // Не мобильная версия
                DeviceScaleFactor = 1  // Масштаб (1 — нормальный, 2 — высокая плотность пикселей)
            });

            // redirect to dashboard url
            Logger.Info("Navigating to dashboard page...");
            await page.GoToAsync(dashboardUrl);
            
            //delay to load dashboard page
            Logger.Info("Delay before open dashboard page...");
            await Task.Delay(3000);

            // Take screen
            Logger.Info($"Taking screenshot and saving to: {filePath}");
            await page.ScreenshotAsync(filePath);

            Logger.Info("Screenshot captured successfully.");
        }
        catch (Exception ex)
        {
            // Error during screen capturing
            Logger.Error(ex, "An error occurred while capturing the screenshot.");
            throw;
        }
    }

}