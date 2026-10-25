using System.Text.Encodings.Web;
using System.Text.Json;
using Steward.Homework.Models;
using Steward.Homework.Services;

namespace Steward.Homework.Storage;

/// <summary>
/// Папка домашних заданий: список занятий (его пишет выгрузка из MTS Link) и выбранные форматы (их пишет приложение)
/// </summary>
/// <param name="directory">Папка, например <c>data/homework</c></param>
public sealed class HomeworkStore(string directory)
{
    /// <summary>
    /// Читает и пишет JSON в camelCase с отступами, кириллица остаётся буквами
    /// </summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Папка домашних заданий
    /// </summary>
    public string Root => directory;

    /// <summary>
    /// Файл со списком занятий
    /// </summary>
    private string LessonsFile => Path.Combine(directory, "lessons.json");

    /// <summary>
    /// Файл с выбранными форматами
    /// </summary>
    private string FormatsFile => Path.Combine(directory, "formats.json");

    /// <summary>
    /// Читает занятия
    /// </summary>
    /// <returns>Занятия от свежих к старым. Пустой список, если файла нет или он испорчен</returns>
    public IReadOnlyList<Lesson> LoadLessons() =>
        [.. (Read<List<Lesson>>(LessonsFile) ?? []).Where(lesson => lesson.Id.Length > 0).OrderByDescending(lesson => lesson.Date)];

    /// <summary>
    /// Читает выбранные форматы
    /// </summary>
    /// <returns>Формат по ключу занятия. Для занятий без выбора ключа нет</returns>
    public IReadOnlyDictionary<string, HomeworkFormatEnum> LoadFormats() =>
        Read<Dictionary<string, HomeworkFormatEnum>>(FormatsFile) ?? [];

    /// <summary>
    /// Формат для занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Выбранный формат или <see cref="HomeworkFormats.DEFAULT"/></returns>
    public HomeworkFormatEnum FormatFor(string lessonId) =>
        LoadFormats().TryGetValue(lessonId, out var format) ? format : HomeworkFormats.DEFAULT;

    /// <summary>
    /// Запоминает формат для занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="format">Выбранный формат</param>
    public void SetFormat(string lessonId, HomeworkFormatEnum format)
    {
        var formats = LoadFormats().ToDictionary(pair => pair.Key, pair => pair.Value);
        formats[lessonId] = format;

        Directory.CreateDirectory(directory);

        var temp = FormatsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(formats, Json));
        File.Move(temp, FormatsFile, overwrite: true);
    }

    /// <summary>
    /// Файл с ключами задач, которые уже созданы в SingularityApp
    /// </summary>
    private string CreatedFile => Path.Combine(directory, "created.json");

    /// <summary>
    /// Читает ключи задач, которые уже созданы
    /// </summary>
    /// <returns>Ключи вида <c>hw:&lt;занятие&gt;:&lt;номер&gt;</c></returns>
    public IReadOnlySet<string> LoadCreated() => Read<List<string>>(CreatedFile)?.ToHashSet(StringComparer.Ordinal) ?? [];

    /// <summary>
    /// Запоминает ключи задач, которые созданы или уже были в SingularityApp
    /// </summary>
    /// <param name="externalIds">Ключи задач</param>
    public void MarkCreated(IEnumerable<string> externalIds)
    {
        var all = LoadCreated().Union(externalIds).Order(StringComparer.Ordinal).ToList();

        Directory.CreateDirectory(directory);

        var temp = CreatedFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(all, Json));
        File.Move(temp, CreatedFile, overwrite: true);
    }

    /// <summary>
    /// Собирает все найденные задания по занятиям от свежих к старым
    /// </summary>
    /// <returns>Задания с форматом и отметкой, создана ли задача. Занятия, по которым заданий ещё не искали, не попадают в список</returns>
    public IReadOnlyList<HomeworkRow> LoadRows()
    {
        var created = LoadCreated();

        return
        [
            .. LoadLessons().SelectMany(lesson =>
            {
                var items = ReadItems(lesson.Id) ?? [];

                return items.Select((item, index) => new HomeworkRow(lesson, FormatFor(lesson.Id), item, index + 1, created.Contains(HomeworkTaskBuilder.ExternalId(lesson, index + 1))));
            })
        ];
    }

    /// <summary>
    /// Путь к расшифровке занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>transcripts/&lt;ключ&gt;.txt</c></returns>
    /// <exception cref="ArgumentException">Ключ не подходит для имени файла</exception>
    public string TranscriptPath(string lessonId) => Path.Combine(directory, "transcripts", SafeName(lessonId) + ".txt");

    /// <summary>
    /// Путь к файлу с заданиями, который записывает агент
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>items/&lt;ключ&gt;.json</c></returns>
    /// <exception cref="ArgumentException">Ключ не подходит для имени файла</exception>
    public string ItemsPath(string lessonId) => Path.Combine(directory, "items", SafeName(lessonId) + ".json");

    /// <summary>
    /// Читает задания занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Задания или <see langword="null"/>, если агент ещё не записал файл</returns>
    public IReadOnlyList<HomeworkItem>? ReadItems(string lessonId) =>
        File.Exists(ItemsPath(lessonId)) ? HomeworkItemParser.Parse(File.ReadAllText(ItemsPath(lessonId))) : null;

    /// <summary>
    /// Проверяет, что ключ можно использовать как имя файла
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Тот же ключ</returns>
    /// <exception cref="ArgumentException">В ключе есть что-то кроме латиницы, цифр, дефиса и подчёркивания</exception>
    private static string SafeName(string lessonId) =>
        lessonId.Length > 0 && lessonId.All(symbol => char.IsAsciiLetterOrDigit(symbol) || symbol is '-' or '_')
            ? lessonId
            : throw new ArgumentException("Ключ занятия может содержать только латиницу, цифры, дефис и подчёркивание", nameof(lessonId));

    /// <summary>
    /// Читает JSON-файл
    /// </summary>
    /// <typeparam name="T">Тип содержимого</typeparam>
    /// <param name="path">Файл</param>
    /// <returns>Содержимое или <see langword="null"/>, если файла нет или он испорчен</returns>
    private static T? Read<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return null;
        }
    }
}