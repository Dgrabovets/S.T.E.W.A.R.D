using System.Text.Encodings.Web;
using System.Text.Json;

namespace Steward.Status.Running;

/// <summary>
/// Журнал запусков в файле <c>runs.jsonl</c>: по одной записи JSON на строку
/// </summary>
public sealed class RunHistory
{
    /// <summary>
    /// Сколько записей оставить, когда файл разросся
    /// </summary>
    private const int KEEP = 300;

    /// <summary>
    /// Размер файла, после которого он обрезается
    /// </summary>
    private const long TRIM_BYTES = 512 * 1024;

    /// <summary>
    /// Пишет и читает JSON в camelCase в одну строку
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Путь к файлу журнала
    /// </summary>
    private readonly string _path;

    /// <summary>
    /// Не даёт двум потокам писать в файл одновременно
    /// </summary>
    private readonly object _gate = new();

    /// <summary>
    /// Создаёт журнал в папке данных
    /// </summary>
    /// <param name="dataDirectory">Папка данных</param>
    public RunHistory(string dataDirectory)
    {
        _path = Path.Combine(dataDirectory, "runs.jsonl");
    }

    /// <summary>
    /// Дописывает запись. Сбой записи не мешает работе: журнал нужен только для показа
    /// </summary>
    /// <param name="record">Запись о запуске</param>
    public void Append(RunRecord record)
    {
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, JsonSerializer.Serialize(record, Json) + Environment.NewLine);
                if (new FileInfo(_path).Length > TRIM_BYTES)
                    File.WriteAllLines(_path, File.ReadLines(_path).TakeLast(KEEP));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // ignore
            }
        }
    }

    /// <summary>
    /// Читает последние записи
    /// </summary>
    /// <param name="count">Сколько записей нужно</param>
    /// <returns>Записи от новых к старым. Повреждённые строки пропускаются</returns>
    public IReadOnlyList<RunRecord> Load(int count)
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path))
                    return [];

                return File.ReadLines(_path)
                    .TakeLast(count)
                    .Select(Parse)
                    .OfType<RunRecord>()
                    .Reverse()
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }

    /// <summary>
    /// Разбирает одну строку журнала
    /// </summary>
    /// <param name="line">Строка файла</param>
    /// <returns>
    /// Запись или <see langword="null"/>, если строка повреждена
    /// </returns>
    private static RunRecord? Parse(string line)
    {
        try
        {
            return JsonSerializer.Deserialize<RunRecord>(line, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
