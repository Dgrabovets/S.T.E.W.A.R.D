namespace MailToSingularity.Interfaces;

/// <summary>
/// Показ уведомления со сводкой
/// </summary>
public interface INotifier
{
    /// <summary>
    /// Показывает уведомление Windows
    /// </summary>
    /// <param name="title">
    /// Заголовок уведомления
    /// </param>
    /// <param name="body">
    /// Текст уведомления
    /// </param>
    void Show(string title, string body);
}
