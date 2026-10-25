using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Steward.Status.State;

/// <summary>
/// Папка с файлами статусов: по одному JSON на процесс
/// </summary>
public sealed partial class StatusStore
{
    /// <summary>
    /// Допустимый идентификатор: он становится именем файла, поэтому путь из него собрать нельзя
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>
    /// Сколько раз повторить замену файла, если читатель держит его в этот момент
    /// </summary>
    private const int WRITE_ATTEMPTS = 5;

    /// <summary>
    /// Читает и пишет JSON в camelCase с отступами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Создаёт хранилище в папке. Папка появляется при первой записи
    /// </summary>
    /// <param name="directory">Папка статусов</param>
    public StatusStore(string directory)
    {
        Directory = directory;
    }

    /// <summary>
    /// Папка статусов
    /// </summary>
    public string Directory { get; }

    #region Writing

    /// <summary>
    /// Записывает статус целиком и атомарно: читатель видит либо старый файл, либо новый
    /// </summary>
    /// <param name="status">Статус процесса</param>
    /// <exception cref="ArgumentException">
    /// Идентификатор не подходит для имени файла
    /// </exception>
    public void Write(ProcessStatus status)
    {
        var path = PathFor(status.Id);
        System.IO.Directory.CreateDirectory(Directory);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(status, Json));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(temp, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < WRITE_ATTEMPTS)
            {
                Thread.Sleep(40 * attempt);
            }
            catch
            {
                File.Delete(temp);
                throw;
            }
        }
    }

    /// <summary>
    /// Записывает состояние процесса, сохраняя из прошлого статуса время последнего успеха, ближайший запуск и число ожидающих
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <param name="title">Название для показа</param>
    /// <param name="state">Новое состояние</param>
    /// <param name="at">Момент записи</param>
    /// <param name="summary">Сводка одной строкой</param>
    /// <param name="error">Текст ошибки</param>
    /// <param name="success">
    /// <see langword="true"/>, если запуск удался и нужно обновить время успеха
    /// </param>
    /// <param name="awaiting">Сколько результатов ждёт решения человека. Если не задано, остаётся прежнее число</param>
    public void Report(string id, string title, ProcessStateEnum state, DateTimeOffset at, string? summary, string? error, bool success, int? awaiting = null)
    {
        var previous = Read(id);
        Write(new ProcessStatus
        {
            Id = id,
            Title = title,
            State = state,
            UpdatedAt = at,
            LastRunAt = state == ProcessStateEnum.Running ? previous?.LastRunAt : at,
            LastSuccessAt = success ? at : previous?.LastSuccessAt,
            NextRunAt = previous?.NextRunAt,
            Summary = summary,
            Error = error,
            Awaiting = awaiting ?? previous?.Awaiting ?? 0,
        });
    }

    #endregion Writing

    #region Reading

    /// <summary>
    /// Читает статус одного процесса
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>
    /// Статус или <see langword="null"/>, если файла нет или он не читается
    /// </returns>
    public ProcessStatus? Read(string id) => ReadFile(PathFor(id));

    /// <summary>
    /// Читает статус и не падает от плохого идентификатора
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>
    /// Статус или <see langword="null"/>, если идентификатор не подходит, файла нет или он не читается
    /// </returns>
    public ProcessStatus? TryRead(string id) => IsValidId(id) ? ReadFile(PathFor(id)) : null;

    /// <summary>
    /// Читает все статусы в папке. Повреждённые и недописанные файлы пропускаются
    /// </summary>
    /// <returns>Статусы по порядку идентификаторов</returns>
    public IReadOnlyList<ProcessStatus> ReadAll()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];

        return System.IO.Directory.EnumerateFiles(Directory, "*.json")
            .Select(ReadFile)
            .OfType<ProcessStatus>()
            .OrderBy(status => status.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Читает один файл, не мешая тому, кто его пишет
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>
    /// Статус или <see langword="null"/>
    /// </returns>
    private static ProcessStatus? ReadFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<ProcessStatus>(stream, Json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    #endregion Reading

    /// <summary>
    /// Проверяет, можно ли использовать идентификатор как имя файла
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>
    /// <see langword="true"/>, если в нём только латиница, цифры, дефис и подчёркивание
    /// </returns>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>
    /// Собирает путь к файлу и отвергает идентификатор с точками, слэшами и прочим
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>Полный путь к файлу статуса</returns>
    /// <exception cref="ArgumentException">
    /// Идентификатор не подходит для имени файла
    /// </exception>
    private string PathFor(string id)
    {
        if (!IdPattern().IsMatch(id))
            throw new ArgumentException($"Идентификатор «{id}» не подходит: нужны латиница, цифры, дефис и подчёркивание", nameof(id));

        return Path.Combine(Directory, id + ".json");
    }
}
