using Steward.Mail.Interfaces;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Записывает показанные уведомления
/// </summary>
public sealed class RecordingNotifier : INotifier
{
    /// <summary>
    /// Показанные уведомления: заголовок и текст
    /// </summary>
    public List<(string Title, string Body)> Shown { get; } = [];

    /// <summary>
    /// Записывает уведомление
    /// </summary>
    /// <param name="title">Заголовок</param>
    /// <param name="body">Текст</param>
    public void Show(string title, string body) => Shown.Add((title, body));
}