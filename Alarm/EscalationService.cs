using NLog;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace JIRAbot.Alarm;

public class EscalationService
{
     private readonly TelegramBotClient _botClient;
        private readonly string _channelId;
        private readonly HttpClient _httpClient;
        private readonly Dictionary<(long chatId, long messageId, string alertId), SosRequest> _sosRequests = new Dictionary<(long, long, string), SosRequest>();
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EscalationService(TelegramBotClient botClient, string channelId)
        {
            _botClient = botClient;
            _channelId = channelId;
            _httpClient = new HttpClient();
        }

        public async Task SendAlertAsync(string message, InlineKeyboardMarkup inlineKeyboard, long originalChatId, int sosMessageId, string userName, string alertId)
        {
            Logger.Info("Sent alarm: {0}, uset: {1}, chat: {2}", message, userName, originalChatId);
            var alertMessage = await _botClient.SendTextMessageAsync(
                chatId: _channelId,
                text: message,
                replyMarkup: inlineKeyboard
            );
            
            Logger.Info("Alarm Sent, ID message: {0}", alertMessage.MessageId);
            
            var sosRequest = new SosRequest
            {
                SosMessageId = sosMessageId,
                ChatId = originalChatId,
                AlertId = alertId
            };
            
            _sosRequests[(originalChatId, sosMessageId, alertId)] = sosRequest;
            
            Logger.Info("Add to Dictionary. message: {0} chatid {1} alertid {2}", sosMessageId, originalChatId, alertId);
     
        }

        public async Task HandleAcceptSos(long chatId, int uniqueMessageId, int alarmMessageId, long alarmChatId, string userName, string channelName, string alertId)
        {
            var sosRequest = _sosRequests.Values.FirstOrDefault(r => r.AlertId == alertId);
            
            if (sosRequest != null)
            {
                Logger.Info("Checked in Dictionary. message {0} chatid {1} alertid {2}",uniqueMessageId,chatId,alertId);

                  await _botClient.EditMessageTextAsync(alarmChatId, alarmMessageId,
                      $"✅ {userName} разбирается с проблемой в группе: {channelName} ");
                  
                  Logger.Info("Alarm updated in the chatid: {0}, messageid: {1}", alarmChatId, alarmMessageId);


                await _botClient.SendTextMessageAsync(
                    chatId: sosRequest.ChatId,
                    text: $" {userName} разбирается с проблемой.",
                    replyToMessageId: sosRequest.SosMessageId
                );


                _sosRequests.Remove((chatId, uniqueMessageId, alertId));
                Logger.Info("Removed from Dictionary. message {0} chatid {1} alertid {2}", uniqueMessageId, chatId, alertId );
            }
            else
            {
                Logger.Error("No active SOS request found for this chat and message ID" );
            }
        }
        
        public class SosRequest
        {
            public int SosMessageId { get; set; }
            public long ChatId { get; set; }
            public string AlertId { get; set; }
        }
    }