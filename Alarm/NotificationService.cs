using NLog;

namespace JIRAbot.Alarm;

public class NotificationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public void SendSms(string alertId)
    {
        Logger.Info("Sending sms. AlertId: {0}", alertId);
    }

    public void MakeCall(string alertId)
    {
        Logger.Info("Calling. AlertId: {0}", alertId);
    }
}