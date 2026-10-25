using System.Text.Encodings.Web;
using System.Text.Json;
using Steward.Homework.Models;

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