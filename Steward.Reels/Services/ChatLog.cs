using System.Text.Json;

namespace Steward.Reels.Services;

/// <summary>
/// Помнит сообщения бота и владельца с временем отправки. Следующий проход с новыми сообщениями их удаляет, а если новых нет, их удаляют, когда подходит срок, который Telegram позволяет
/// </summary>
/// <param name="path">Файл со списком сообщений</param>
public sealed class ChatLog(string path)
{
    /// <summary>
    /// Читает запомненные сообщения. В старом формате без времени берётся время записи файла
    /// </summary>
    /// <returns>Чат, номер сообщения в нём и когда оно отправлено</returns>
    public IReadOnlyList<(long ChatId, long MessageId, DateTimeOffset SentAt)> Read()
    {
        try
        {
            if (!File.Exists(path))
                return [];

            var fallback = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);

            return [.. (JsonSerializer.Deserialize<long[][]>(File.ReadAllText(path)) ?? []).Where(item => item.Length >= 2).Select(item => (item[0], item[1], item.Length >= 3 ? DateTimeOffset.FromUnixTimeSeconds(item[2]) : fallback))];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Добавляет сообщение к запомненным
    /// </summary>
    /// <param name="chatId">Чат</param>
    /// <param name="messageId">Номер сообщения в чате. Ноль не запоминается</param>
    /// <param name="sentAt">Когда сообщение отправлено</param>
    public void Add(long chatId, long messageId, DateTimeOffset sentAt)
    {
        if (messageId != 0)
            Write([.. Read(), (chatId, messageId, sentAt)]);
    }

    /// <summary>
    /// Забывает сообщения, которые уже удалены или удалить нельзя
    /// </summary>
    /// <param name="forgotten">Сообщения, которые нужно убрать из списка</param>
    public void Forget(IEnumerable<(long ChatId, long MessageId, DateTimeOffset SentAt)> forgotten) => Write(Read().Except(forgotten));

    /// <summary>
    /// Записывает список через временный файл
    /// </summary>
    /// <param name="items">Чат, номер сообщения и время отправки</param>
    private void Write(IEnumerable<(long ChatId, long MessageId, DateTimeOffset SentAt)> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items.Select(item => new[] { item.ChatId, item.MessageId, item.SentAt.ToUnixTimeSeconds() })));
        File.Move(temp, path, overwrite: true);
    }
}
