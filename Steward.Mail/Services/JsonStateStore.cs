using Steward.Mail.Interfaces;
using System.Text.Json;

namespace Steward.Mail.Services;

/// <summary>
/// Список обработанных Message-ID в JSON-файле рядом с exe
/// </summary>
public sealed class JsonStateStore : IStateStore
{
    /// <summary>
    /// Сколько последних идентификаторов хранить в файле, чтобы он не рос бесконечно
    /// </summary>
    private const int MAX_ENTRIES = 2000;

    /// <summary>
    /// Путь к JSON-файлу
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// Идентификаторы в порядке добавления: по нему отрезается старый хвост
    /// </summary>
    private readonly List<string> _ids;

    /// <summary>
    /// Те же идентификаторы для быстрой проверки
    /// </summary>
    private readonly HashSet<string> _lookup;

    /// <summary>
    /// Загружает список из файла. Если файла нет, список пуст
    /// </summary>
    /// <param name="path">Путь к JSON-файлу</param>
    public JsonStateStore(string path)
    {
        _path = path;
        _ids = File.Exists(path)
            ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? []
            : [];

        _lookup = [.. _ids];
    }

    /// <summary>
    /// Проверяет, обработано ли письмо раньше
    /// </summary>
    /// <param name="messageId">Идентификатор письма из заголовка Message-ID</param>
    /// <returns>
    /// <see langword="true"/>, если идентификатор уже есть в списке
    /// </returns>
    public bool IsProcessed(string messageId) => _lookup.Contains(messageId);

    /// <summary>
    /// Добавляет идентификатор в список. Повторное добавление ничего не меняет
    /// </summary>
    /// <param name="messageId">Идентификатор письма из заголовка Message-ID</param>
    public void MarkProcessed(string messageId)
    {
        if (_lookup.Add(messageId))
            _ids.Add(messageId);
    }

    /// <summary>
    /// Записывает список в файл через временный файл, чтобы обрыв записи не испортил список. Остаются только последние <see cref="MAX_ENTRIES"/> идентификаторов
    /// </summary>
    public void Save()
    {
        var tail = _ids.Count > MAX_ENTRIES ? _ids[^MAX_ENTRIES..] : _ids;
        var temp = _path + ".tmp";

        File.WriteAllText(temp, JsonSerializer.Serialize(tail, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _path, overwrite: true);
    }
}
