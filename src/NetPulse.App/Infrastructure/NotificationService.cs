using System;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;

namespace NetPulse.App.Infrastructure
{
    public class NotificationService
    {
        private Action<string, string>? _balloonFallbackAction;

        public void RegisterBalloonFallback(Action<string, string> fallbackAction)
        {
            _balloonFallbackAction = fallbackAction;
        }

        public async Task ShowToastAsync(string title, string message)
        {
            await Task.Run(() =>
            {
                try
                {
                    new ToastContentBuilder()
                        .AddText(title)
                        .AddText(message)
                        .Show();
                }
                catch (Exception)
                {
                    // If registry, permissions, or Windows version prevents toast, fallback
                    ShowTrayBalloon(title, message);
                }
            });
        }

        public void ShowTrayBalloon(string title, string message)
        {
            _balloonFallbackAction?.Invoke(title, message);
        }
    }
}
