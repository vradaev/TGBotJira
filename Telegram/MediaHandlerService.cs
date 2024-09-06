using NLog;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace JIRAbot;

public class MediaHandlerService
{
    private readonly TelegramBotClient _botClient;
    private readonly JiraClient _jiraClient;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public MediaHandlerService(TelegramBotClient botClient, JiraClient jiraClient)
    {
        _botClient = botClient;
        _jiraClient = jiraClient;
    }

    public async Task HandleMediaMessageAsync(Message message, string botUsername, Dictionary<long, string> messageToIssueMap, ChatConfig chatConfig)
    {
        Logger.Info("Received media message from chat {0}", message.Chat.Id);
        
        if (chatConfig == null)
        {
            Logger.Info("Ignoring media message from chat {0} as it is not configured.", message.Chat.Id);
            return;
        }

        if (message.Caption == null || !message.Caption.Contains($"@{botUsername}", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("Ignoring media message from chat {0} as it does not mention the bot.", message.Chat.Id);
            return;
        }

        var fileId = message.Document?.FileId ?? message.Photo?.LastOrDefault()?.FileId;
        if (fileId == null)
        {
            Logger.Error("No fileId found in the media message from chat {0}.", message.Chat.Id);
            await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab No fileId found in the message.");
            return;
        }

        var filePath = await _botClient.GetFileAsync(fileId);
        if (filePath == null)
        {
            Logger.Error("Failed to retrieve file path for fileId {0} from chat {1}.", fileId, message.Chat.Id);
            await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab Failed to retrieve the file.");
            return;
        }

        var fileName = message.Document?.FileName ?? "image.jpg";

        try
        {
            using (var saveImageStream = new FileStream(fileName, FileMode.Create))
            {
                await _botClient.DownloadFileAsync(filePath.FilePath, saveImageStream);
            }

            Logger.Info("File {0} saved successfully from chat {1}.", fileName, message.Chat.Id);

            if (message.ReplyToMessage != null)
            {
                if (messageToIssueMap.TryGetValue(message.ReplyToMessage.MessageId, out string existingIssueKey))
                {
                    await AttachFilesToIssueAsync(message, existingIssueKey);
                    var commentText = message.Caption?.Replace($"@{botUsername}", "", StringComparison.OrdinalIgnoreCase).Trim() ?? string.Empty;
                    await _jiraClient.AddCommentToIssueAsync(existingIssueKey, commentText);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, $"\ud83d\udcdd Comment added with attachment to: <a href=\"https://ct-ms.atlassian.net/browse/{existingIssueKey}\">{existingIssueKey}</a>", parseMode: ParseMode.Html);
                    Logger.Info("Added comment with attachment to Jira issue {0} from chat {1}", existingIssueKey, message.Chat.Id);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "\u2757 Please provide a description of the issue after mentioning the bot.");
                }
            }
            else
            {
                var cleanedText = message.Caption.Replace($"@{botUsername}", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (string.IsNullOrEmpty(cleanedText))
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "\u2757 Please provide a description of the issue after mentioning the bot.");
                }
                else
                {
                    string summary = cleanedText.Length > 250 ? cleanedText.Substring(0, 250) : cleanedText;
                    string newIssueKey = await _jiraClient.CreateIssueAsync(summary, cleanedText, chatConfig.ClientName);

                    if (!string.IsNullOrEmpty(newIssueKey))
                    {
                        await AttachFilesToIssueAsync(message, newIssueKey);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, $"\ud83c\udd95 Issue created: <a href=\"https://ct-ms.atlassian.net/browse/{newIssueKey}\">{newIssueKey}</a>", parseMode: ParseMode.Html);
                        messageToIssueMap[message.MessageId] = newIssueKey;
                        Logger.Info("Issue created in Jira with attachment: {0}", newIssueKey);
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab Failed to create issue.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error while handling media message.");
            await _botClient.SendTextMessageAsync(message.Chat.Id, "\ud83d\udeab An error occurred while processing your media message.");
        }
    }

    private async Task AttachFilesToIssueAsync(Message message, string issueKey)
    {
        string fileId = message.Document?.FileId ?? message.Photo?.LastOrDefault()?.FileId;
        string fileName = message.Document?.FileName ?? "image.jpg";

        var filePath = await _botClient.GetFileAsync(fileId);
        if (filePath != null)
        {
            using (var saveImageStream = new FileStream(fileName, FileMode.Create))
            {
                await _botClient.DownloadFileAsync(filePath.FilePath, saveImageStream);
            }

            await _jiraClient.AttachFileToIssueAsync(issueKey, fileName);
            Logger.Info("Attached file to Jira issue {0}: {1}", issueKey, fileName);
        }
    }
}