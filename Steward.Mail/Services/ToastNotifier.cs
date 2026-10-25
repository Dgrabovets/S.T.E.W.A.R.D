using Microsoft.Toolkit.Uwp.Notifications;
using Steward.Mail.Interfaces;

namespace Steward.Mail.Services;

/// <summary>
/// Системное уведомление Windows
/// </summary>
public sealed class ToastNotifier : INotifier
{
    /// <summary>
    /// Показывает уведомление Windows из двух строк
    /// </summary>
    /// <param name="title">Заголовок уведомления</param>
    /// <param name="body">Текст уведомления</param>
    public void Show(string title, string body)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(body)
            .Show();
    }
}
