using System.Text.Json;
using Steward.Mail.Interfaces;

namespace Steward.Mail.Services;

/// <summary>
/// Хранит идентификаторы разобранных писем в файле. Ящик при этом не меняется: письмо остаётся непрочитанным
/// </summary>
public sealed class HandledMailFile : IHandledMail
{
    /// <summary>
    /// Сколько идентификаторов держать: старые письма в ящик уже не попадают, потому что поиск идёт по последним дням
    /// </summary>
    private const int LIMIT = 1000;

    /// <summary>
    /// Путь к файлу
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// Идентификаторы от старых к новым
    /// </summary>
    private readonly List<string> _ids;

    /// <summary>
    /// Читает файл. Если его нет или он испорчен, начинает с пустого списка
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    public HandledMailFile(string path)
    {
        _path = path;
        _ids = Load(path);
    }

    /// <summary>
    /// Проверяет, что письмо уже разбирали
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    /// <returns>
    /// <see langword="true"/>, если письмо уже разобрано
    /// </returns>
    public bool Contains(string messageId) => _ids.Contains(messageId, StringComparer.Ordinal);

    /// <summary>
    /// Запоминает письмо и сразу записывает файл, чтобы сбой на следующем письме не потерял уже разобранные
    /// </summary>
    /// <param name="messageId">Идентификатор письма</param>
    public void Add(string messageId)
    {
        if (Contains(messageId))
            return;

        _ids.Add(messageId);
        if (_ids.Count > LIMIT)
            _ids.RemoveRange(0, _ids.Count - LIMIT);

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_ids));
    }

    /// <summary>
    /// Читает список из файла
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Идентификаторы. Пустой список, если файла нет или он не читается</returns>
    private static List<string> Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? [] : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }
}
