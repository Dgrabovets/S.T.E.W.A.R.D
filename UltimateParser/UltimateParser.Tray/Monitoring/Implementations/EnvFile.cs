using System.IO;

namespace UltimateParser.Tray.Monitoring.Implementations;

/// <summary>
/// Читает и записывает пары из файла <c>.env</c>, не печатая значения
/// </summary>
public sealed class EnvFile
{
    /// <summary>
    /// Строки файла в исходном порядке
    /// </summary>
    private readonly List<string> _lines;

    /// <summary>
    /// Путь к файлу
    /// </summary>
    private readonly string _path;

    #region Loading

    /// <summary>
    /// Открывает файл настроек
    /// </summary>
    /// <param name="path">
    /// Полный путь к <c>.env</c>
    /// </param>
    /// <param name="lines">
    /// Строки файла
    /// </param>
    private EnvFile(string path, List<string> lines)
    {
        _path = path;
        _lines = lines;
    }

    /// <summary>
    /// Загружает файл. Если его нет, начинает с пустого списка
    /// </summary>
    /// <param name="path">
    /// Полный путь к <c>.env</c>
    /// </param>
    /// <returns>
    /// Набор строк
    /// </returns>
    public static EnvFile Load(string path)
    {
        var lines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : [];
        return new EnvFile(path, lines);
    }

    /// <summary>
    /// Возвращает значение ключа
    /// </summary>
    /// <param name="key">
    /// Имя переменной
    /// </param>
    /// <returns>
    /// Значение без кавычек. Пусто, если ключа нет
    /// </returns>
    public string Get(string key)
    {
        foreach (var line in _lines)
        {
            if (!TrySplit(line, out var name, out var value) || !string.Equals(name, key, StringComparison.Ordinal))
                continue;

            return value;
        }

        return string.Empty;
    }

    #endregion Loading

    #region Saving

    /// <summary>
    /// Заменяет значение ключа или дописывает его в конец
    /// </summary>
    /// <param name="key">
    /// Имя переменной
    /// </param>
    /// <param name="value">
    /// Новое значение
    /// </param>
    public void Set(string key, string value)
    {
        if (value.Contains('\r') || value.Contains('\n'))
            throw new InvalidOperationException("Значение настройки не должно содержать перевод строки");

        var rendered = key + "=" + Quote(value);
        for (var index = 0; index < _lines.Count; index++)
        {
            if (!TrySplit(_lines[index], out var name, out _) || !string.Equals(name, key, StringComparison.Ordinal))
                continue;

            var indent = _lines[index].Length - _lines[index].TrimStart().Length;
            _lines[index] = _lines[index][..indent] + rendered;
            return;
        }

        _lines.Add(rendered);
    }

    /// <summary>
    /// Записывает файл на диск
    /// </summary>
    public void Save()
    {
        File.WriteAllLines(_path, _lines);
    }

    #endregion Saving

    #region Lines

    /// <summary>
    /// Делит строку на имя и значение
    /// </summary>
    /// <param name="line">
    /// Строка файла
    /// </param>
    /// <param name="key">
    /// Имя слева от знака равенства
    /// </param>
    /// <param name="value">
    /// Значение без обрамляющих кавычек
    /// </param>
    /// <returns>
    /// <see langword="false"/> для пустых строк и комментариев
    /// </returns>
    private static bool TrySplit(string line, out string key, out string value)
    {
        var trimmed = line.Trim();
        var split = trimmed.IndexOf('=');
        if (split <= 0 || trimmed.StartsWith('#'))
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = trimmed[..split].Trim();
        var raw = trimmed[(split + 1)..].Trim();
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            raw = raw[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

        value = raw;
        return key.Length > 0;
    }

    /// <summary>
    /// Берёт значение в кавычки, если в нём есть пробел, решётка или знак равенства. Перевод строки сюда не доходит
    /// </summary>
    /// <param name="value">
    /// Исходный текст
    /// </param>
    /// <returns>
    /// Фрагмент справа от знака равенства
    /// </returns>
    private static string Quote(string value)
    {
        if (value.IndexOfAny([' ', '\t', '#', '"', '=']) < 0)
            return value;

        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    #endregion Lines
}
