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
        private readonly NotificationService _notificationService;

        public EscalationService(TelegramBotClient botClient, string channelId, NotificationService notificationService)
        {
            _botClient = botClient;
            _channelId = channelId;
            _httpClient = new HttpClient();
            _notificationService = notificationService;
        }

        public async Task SendAlertAsync(string message, InlineKeyboardMarkup inlineKeyboard, long originalChatId, int sosMessageId, string userName, string alertId)
        {
            Logger.Info("Sent alert: {0}, chat: {1}, alertId:{2} ", message, originalChatId, alertId);
            var alertMessage = await _botClient.SendTextMessageAsync(
                chatId: _channelId,
                text: message,
                replyMarkup: inlineKeyboard
            );
            
            var cancellationTokenSource = new CancellationTokenSource();
            var sosRequest = new SosRequest
            {
                SosMessageId = sosMessageId,
                ChatId = originalChatId,
                AlertId = alertId,
                CancellationTokenSource = cancellationTokenSource
            };
            
            _sosRequests[(originalChatId, sosMessageId, alertId)] = sosRequest;
            
            Logger.Info("Add to Dictionary. message: {0} chatid {1} alertid {2}", sosMessageId, originalChatId, alertId);
            
            _ = StartTimerForSmsAsync(sosRequest, cancellationTokenSource.Token);
            Logger.Info("StartTimerForSms. message: {0} chatid {1} alertid {2}", sosMessageId, originalChatId, alertId);
            
            _ = StartTimerForCallAsync(sosRequest, cancellationTokenSource.Token);
            Logger.Info("StartTimerForCall. message: {0} chatid {1} alertid {2}", sosMessageId, originalChatId, alertId);
     
        }

        public async Task HandleAcceptSos(long chatId, int uniqueMessageId, int alarmMessageId, long alarmChatId, string userName, string channelName, string alertId)
        {
            var sosRequest = _sosRequests.Values.FirstOrDefault(r => r.AlertId == alertId);
            
            if (sosRequest != null)
            {
                sosRequest.IsAccepted = true; // Устанавливаем флаг принятия
                sosRequest.CancellationTokenSource.Cancel(); // Отменяем оба таймера

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
        private async Task StartTimerForSmsAsync(SosRequest sosRequest, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                
                if (!sosRequest.IsAccepted)
                {
                    _notificationService.SendSms(sosRequest.AlertId);
                    Logger.Info("SMS sent. AlertId: {0}", sosRequest.AlertId);
                }
            }
            catch (TaskCanceledException)
            {

                Logger.Info("Canceled timer for the sms. AlertId: {0}", sosRequest.AlertId);
            }
        }
        
        private async Task StartTimerForCallAsync(SosRequest sosRequest, CancellationToken cancellationToken)
        {
            try
            {

                await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);

                // Если тревога не принята, совершаем звонок
                if (!sosRequest.IsAccepted)
                {
                    _notificationService.MakeCall(sosRequest.AlertId);
                    Logger.Info("Called. AlertId: {0}", sosRequest.AlertId);
                }
            }
            catch (TaskCanceledException)
            {
                // Таймер был отменен
                Logger.Info("Canceled timer for the call. AlertId: {0}", sosRequest.AlertId);
            }
        }
        
        
        public class SosRequest
        {
            public int SosMessageId { get; set; }
            public long ChatId { get; set; }
            public string AlertId { get; set; }
            public bool IsAccepted { get; set; } = false; 
            public CancellationTokenSource CancellationTokenSource { get; set; } 
            
        }
    }