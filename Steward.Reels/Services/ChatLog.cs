using System.Text.Json;

namespace Steward.Reels.Services;

/// <summary>
/// Помнит сообщения последнего прохода бота: свои ответы и сообщения владельца. Следующий проход их удаляет, чтобы чат оставался чистым
/// </summary>
/// <param name="path">Файл со списком сообщений</param>
public sealed class ChatLog(string path)
{
    /// <summary>
    /// Читает запомненные сообщения
    /// </summary>
    /// <returns>Чат и номер сообщения в нём</returns>
    public IReadOnlyList<(long ChatId, long MessageId)> Read()
    {
        try
        {
            return File.Exists(path) ? [.. (JsonSerializer.Deserialize<long[][]>(File.ReadAllText(path)) ?? []).Where(pair => pair.Length == 2).Select(pair => (pair[0], pair[1]))] : [];
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
    public void Add(long chatId, long messageId)
    {
        if (messageId != 0)
            Write([.. Read(), (chatId, messageId)]);
    }

    /// <summary>
    /// Забывает все сообщения
    /// </summary>
    public void Clear() => Write([]);

    /// <summary>
    /// Записывает список через временный файл
    /// </summary>
    /// <param name="items">Чат и номер сообщения</param>
    private void Write(IEnumerable<(long ChatId, long MessageId)> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(items.Select(item => new[] { item.ChatId, item.MessageId })));
        File.Move(temp, path, overwrite: true);
    }
}
