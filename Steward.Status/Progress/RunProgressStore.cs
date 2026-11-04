using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Steward.Status.Progress;

/// <summary>
/// Файлы прогресса запусков: по одному JSON на процесс в подпапке <c>progress</c> папки статусов. Подпапка нужна, чтобы обычный список статусов не принял их за статусы процессов
/// </summary>
public sealed partial class RunProgressStore
{
    /// <summary>
    /// Допустимый идентификатор: он становится именем файла, поэтому путь из него собрать нельзя
    /// </summary>
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>
    /// Имя подпапки с файлами прогресса
    /// </summary>
    private const string FOLDER = "progress";

    /// <summary>
    /// Читает и пишет JSON в camelCase
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Папка с файлами прогресса
    /// </summary>
    private readonly string _directory;

    /// <summary>
    /// Создаёт хранилище рядом с файлами статусов. Папка появляется при первой записи
    /// </summary>
    /// <param name="statusDirectory">Папка статусов</param>
    public RunProgressStore(string statusDirectory)
    {
        _directory = Path.Combine(statusDirectory, FOLDER);
    }

    /// <summary>
    /// Записывает прогресс целиком и атомарно: читатель видит либо старый файл, либо новый
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <param name="progress">Прогресс</param>
    /// <exception cref="ArgumentException">
    /// Идентификатор не подходит для имени файла
    /// </exception>
    public void Write(string id, RunProgress progress)
    {
        var path = PathFor(id);
        Directory.CreateDirectory(_directory);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(progress, Json));
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    /// <summary>
    /// Читает прогресс процесса
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>Прогресс или <see langword="null"/>, если файла нет, он не читается или в нём нет шагов</returns>
    public RunProgress? Read(string id)
    {
        try
        {
            var path = PathFor(id);
            if (!File.Exists(path))
                return null;

            var progress = JsonSerializer.Deserialize<RunProgress>(File.ReadAllText(path), Json);

            return progress is { Stages.Count: > 0 } ? progress : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Удаляет файл прогресса: запуск закончился
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    public void Clear(string id)
    {
        try
        {
            var path = PathFor(id);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Файл прогресса вспомогательный: не удалился сейчас, перезапишется или устареет сам
        }
    }

    /// <summary>
    /// Собирает путь к файлу и отвергает идентификатор с точками, слэшами и прочим
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <returns>Полный путь к файлу прогресса</returns>
    /// <exception cref="ArgumentException">
    /// Идентификатор не подходит для имени файла
    /// </exception>
    private string PathFor(string id)
    {
        if (!IdPattern().IsMatch(id))
            throw new ArgumentException($"Идентификатор «{id}» не подходит: нужны латиница, цифры, дефис и подчёркивание", nameof(id));

        return Path.Combine(_directory, id + ".json");
    }
}
