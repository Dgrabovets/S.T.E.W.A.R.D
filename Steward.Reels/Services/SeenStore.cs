using System.Text.Json;

namespace Steward.Reels.Services;

/// <summary>
/// Помнит ссылки, которые уже превращались в идеи, даже если идею потом удалили, и вопросы о повторе, которые ждут ответа
/// </summary>
/// <param name="seenPath">Файл со списком обработанных рилсов</param>
/// <param name="pendingPath">Файл с вопросами о повторе</param>
public sealed class SeenStore(string seenPath, string pendingPath)
{
    /// <summary>
    /// Читает ключи уже обработанных рилсов
    /// </summary>
    /// <returns>Ключи, как их строит <see cref="ReelLinks.Key"/></returns>
    public HashSet<string> Keys() => File.Exists(seenPath) ? [.. File.ReadAllLines(seenPath).Where(line => line.Length > 0)] : [];

    /// <summary>
    /// Добавляет рилс к обработанным
    /// </summary>
    /// <param name="key">Ключ рилса</param>
    public void Add(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(seenPath)!);
        if (!Keys().Contains(key))
            File.AppendAllLines(seenPath, [key]);
    }

    /// <summary>
    /// Запоминает вопрос «добавить повторно?» до ответа
    /// </summary>
    /// <param name="token">Метка вопроса, она же в кнопках</param>
    /// <param name="url">Ссылка, о которой спросили</param>
    public void AddPending(string token, string url)
    {
        var pending = ReadPending();
        pending[token] = url;
        WritePending(pending);
    }

    /// <summary>
    /// Забирает вопрос: после этого на него нельзя ответить второй раз
    /// </summary>
    /// <param name="token">Метка вопроса</param>
    /// <returns>Ссылка или <see langword="null"/>, если вопроса нет или на него уже ответили</returns>
    public string? TakePending(string token)
    {
        var pending = ReadPending();
        if (!pending.Remove(token, out var url))
            return null;

        WritePending(pending);

        return url;
    }

    /// <summary>
    /// Читает вопросы, которые ждут ответа
    /// </summary>
    /// <returns>Метка и ссылка</returns>
    private Dictionary<string, string> ReadPending()
    {
        try
        {
            return File.Exists(pendingPath) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(pendingPath)) ?? [] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// Записывает вопросы через временный файл
    /// </summary>
    /// <param name="pending">Метка и ссылка</param>
    private void WritePending(Dictionary<string, string> pending)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pendingPath)!);

        var temp = pendingPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(pending));
        File.Move(temp, pendingPath, overwrite: true);
    }
}
