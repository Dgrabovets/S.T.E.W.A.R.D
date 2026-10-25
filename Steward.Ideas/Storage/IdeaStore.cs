using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steward.Ideas.Models;

namespace Steward.Ideas.Storage;

/// <summary>
/// Папка с идеями: по одному JSON-файлу на идею. В неё пишет бот, а читает и решает приложение в трее
/// </summary>
public sealed partial class IdeaStore
{
    /// <summary>
    /// Сколько раз повторить замену файла, если читатель держит его в этот момент
    /// </summary>
    private const int WRITE_ATTEMPTS = 5;

    /// <summary>
    /// Читает и пишет JSON в camelCase с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Создаёт хранилище в папке. Папка появляется при первой записи
    /// </summary>
    /// <param name="directory">Папка идей</param>
    public IdeaStore(string directory)
    {
        Directory = directory;
    }

    /// <summary>
    /// Папка идей
    /// </summary>
    public string Directory { get; }

    /// <summary>
    /// Проверяет, можно ли использовать идентификатор как имя файла
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <returns>
    /// <see langword="true"/>, если в нём только латиница, цифры, дефис и подчёркивание
    /// </returns>
    public static bool IsValidId(string? id) => id is not null && IdPattern().IsMatch(id);

    /// <summary>
    /// Путь к файлу идеи
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <returns>Полный путь к <c>&lt;id&gt;.json</c></returns>
    /// <exception cref="ArgumentException">Идентификатор не подходит для имени файла</exception>
    public string PathFor(string id)
    {
        if (!IsValidId(id))
            throw new ArgumentException("Идентификатор идеи может содержать только латиницу, цифры, дефис и подчёркивание", nameof(id));

        return Path.Combine(Directory, id + ".json");
    }

    /// <summary>
    /// Сообщает, есть ли уже такая идея
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <returns>
    /// <see langword="true"/>, если файл идеи существует
    /// </returns>
    public bool Exists(string id) => File.Exists(PathFor(id));

    /// <summary>
    /// Записывает идею целиком через временный файл, чтобы читатель не увидел недописанное
    /// </summary>
    /// <param name="idea">Идея</param>
    public void Save(Idea idea)
    {
        var path = PathFor(idea.Id);
        System.IO.Directory.CreateDirectory(Directory);

        for (var attempt = 1; ; attempt++)
        {
            var temp = $"{path}.{Environment.ProcessId}.tmp";
            try
            {
                File.WriteAllText(temp, JsonSerializer.Serialize(idea, Json));
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
    /// Читает все идеи. Повреждённые и недописанные файлы пропускаются
    /// </summary>
    /// <returns>Идеи от новых к старым</returns>
    public IReadOnlyList<Idea> LoadAll()
    {
        if (!System.IO.Directory.Exists(Directory))
            return [];

        return [.. System.IO.Directory.EnumerateFiles(Directory, "*.json").Select(ReadFile).OfType<Idea>().OrderByDescending(idea => idea.CreatedAt)];
    }

    /// <summary>
    /// Меняет решение по идее
    /// </summary>
    /// <param name="id">Идентификатор идеи</param>
    /// <param name="state">Новое решение</param>
    /// <returns>
    /// <see langword="true"/>, если идея нашлась и обновилась
    /// </returns>
    public bool SetState(string id, IdeaStateEnum state)
    {
        if (!IsValidId(id) || ReadFile(PathFor(id)) is not { } idea)
            return false;

        Save(idea with { State = state });

        return true;
    }

    /// <summary>
    /// Читает один файл, не мешая тому, кто его пишет
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Идея или <see langword="null"/>, если файла нет или он не читается</returns>
    private static Idea? ReadFile(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            return JsonSerializer.Deserialize<Idea>(stream, Json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Допустимый идентификатор: он становится именем файла, поэтому путь из него собрать нельзя
    /// </summary>
    /// <returns>Регулярное выражение идентификатора</returns>
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}