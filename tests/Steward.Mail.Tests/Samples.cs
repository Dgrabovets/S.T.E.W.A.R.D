using Steward.Mail.Models;

namespace Steward.Mail.Tests;

/// <summary>
/// Общие заготовки для тестов
/// </summary>
public static class Samples
{
    /// <summary>
    /// Письмо из «Входящих» с настраиваемыми полями
    /// </summary>
    /// <param name="id">Идентификатор письма</param>
    /// <param name="fromName">Имя отправителя</param>
    /// <param name="subject">Тема</param>
    /// <param name="hour">Час получения 25 октября 2026 года</param>
    /// <param name="folder">Папка ящика</param>
    /// <param name="fromAddress">Адрес отправителя</param>
    /// <returns>Письмо</returns>
    public static MailItem Mail(string id, string fromName = "Иванов Иван Иванович", string subject = "Вопрос", int hour = 9, string folder = "INBOX", string fromAddress = "ivanov@example.com") =>
        new(id, folder, fromName, fromAddress, subject, new DateTimeOffset(2026, 10, 25, hour, 0, 0, TimeSpan.Zero), "Начало письма");
}