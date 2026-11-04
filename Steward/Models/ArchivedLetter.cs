using System.IO;
using System.Text.Json;

namespace Steward.Models;

/// <summary>
/// Важное письмо из архива, который пишет программа почты (<c>data/mail/archive.json</c>)
/// </summary>
/// <param name="MessageId">Идентификатор письма</param>
/// <param name="Kind">Тип: «Ответить» или «Отзыв»</param>
/// <param name="FromName">Имя отправителя</param>
/// <param name="FromAddress">Адрес отправителя</param>
/// <param name="Subject">Тема письма</param>
/// <param name="ReceivedAt">Когда письмо пришло</param>
/// <param name="HandledAt">Когда программа его разобрала</param>
/// <param name="Preview">Начало текста письма</param>
/// <param name="Link">Ссылка на письмо в веб-почте. Пусто, если её нет</param>
/// <param name="TaskCreated">По письму заведена задача в Singularity</param>
public sealed record ArchivedLetter(string MessageId, string Kind, string FromName, string FromAddress, string Subject, DateTimeOffset ReceivedAt, DateTimeOffset HandledAt, string Preview, string Link, bool TaskCreated)
{
    /// <summary>
    /// Читает архив писем. Нет файла или он испорчен: архив пуст, а страница показывает письма из журнала
    /// </summary>
    /// <param name="path">Файл архива</param>
    /// <returns>Письма от новых к старым</returns>
    public static IReadOnlyList<ArchivedLetter> Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<List<ArchivedLetter>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
