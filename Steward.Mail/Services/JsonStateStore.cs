using System.Text.Json;
using Steward.Mail.Interfaces;

namespace Steward.Mail.Services;

/// <summary>
/// Список обработанных Message-ID в JSON-файле рядом с exe
/// </summary>
public sealed class JsonStateStore : IStateStore
{
    #region Fields

    /// <summary>
    /// Сколько последних идентификаторов хранить в файле, чтобы он не рос бесконечно
    /// </summary>
    private const int MaxEntries = 2000;

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

    #endregion

    #region Constructors

    /// <summary>
    /// Загружает список из файла. Если файла нет, список пуст
    /// </summary>
    /// <param name="path">
    /// Путь к JSON-файлу
    /// </param>
    public JsonStateStore(string path)
    {
        _path = path;
        _ids = File.Exists(path)
            ? JsonSerializer.Deserialize<List<string>>(File.ReadAllText(path)) ?? []
            : [];
        _lookup = [.. _ids];
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Проверяет, обработано ли письмо раньше
    /// </summary>
    /// <param name="messageId">
    /// Идентификатор письма из заголовка Message-ID
    /// </param>
    /// <returns>
    /// <see langword="true"/>, если идентификатор уже есть в списке
    /// </returns>
    public bool IsProcessed(string messageId) => _lookup.Contains(messageId);

    /// <summary>
    /// Добавляет идентификатор в список. Повторное добавление ничего не меняет
    /// </summary>
    /// <param name="messageId">
    /// Идентификатор письма из заголовка Message-ID
    /// </param>
    public void MarkProcessed(string messageId)
    {
        if (_lookup.Add(messageId))
        {
            _ids.Add(messageId);
        }
    }

    /// <summary>
    /// Записывает список в файл, оставляя только последние <see cref="MaxEntries"/> идентификаторов
    /// </summary>
    public void Save()
    {
        var tail = _ids.Count > MaxEntries ? _ids[^MaxEntries..] : _ids;
        File.WriteAllText(_path, JsonSerializer.Serialize(tail, new JsonSerializerOptions { WriteIndented = true }));
    }

    #endregion
}
