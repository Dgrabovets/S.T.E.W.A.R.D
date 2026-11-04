using System.Text.Json;

namespace Steward.Status.Registry;

/// <summary>
/// Выключатели задач, которые ставит владелец в окне: включена задача или нет. Хранятся в отдельном файле, чтобы не переписывать <c>config/jobs.json</c> с его комментариями. Выключатель сильнее поля <c>enabled</c> из реестра
/// </summary>
public sealed class JobSwitches
{
    /// <summary>
    /// Читает и пишет JSON в camelCase
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>
    /// Файл выключателей
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// Создаёт выключатели в файле. Файл появляется при первой записи
    /// </summary>
    /// <param name="path">Файл выключателей</param>
    public JobSwitches(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Читает выключатели
    /// </summary>
    /// <returns>Идентификатор задачи и включена ли она. Пустой словарь, если файла нет или он не читается</returns>
    public IReadOnlyDictionary<string, bool> Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(_path), Json) is { } found ? new Dictionary<string, bool>(found, StringComparer.Ordinal) : []
                : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, bool>(StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Применяет выключатели к задачам из реестра
    /// </summary>
    /// <param name="jobs">Задачи из реестра</param>
    /// <returns>Те же задачи, у которых <see cref="JobDefinition.Enabled"/> заменён выключателем владельца, если он есть</returns>
    public IReadOnlyList<JobDefinition> Apply(IReadOnlyList<JobDefinition> jobs)
    {
        var switches = Read();

        return [.. jobs.Select(job => switches.TryGetValue(job.Id, out var enabled) ? job with { Enabled = enabled } : job)];
    }

    /// <summary>
    /// Записывает выключатель одной задачи, не трогая остальные. Запись атомарная
    /// </summary>
    /// <param name="id">Идентификатор задачи</param>
    /// <param name="enabled">Включена ли задача</param>
    /// <exception cref="IOException">Файл не записался</exception>
    public void Set(string id, bool enabled)
    {
        var all = new Dictionary<string, bool>(Read(), StringComparer.Ordinal) { [id] = enabled };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temp = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(all, Json));
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
