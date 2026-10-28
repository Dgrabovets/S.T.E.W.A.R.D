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
    /// Формат для занятия: тот, в котором занятие готовили, а если не готовили, формат по умолчанию для его предмета
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Формат занятия, формат предмета или <see cref="HomeworkFormats.DEFAULT"/></returns>
    public HomeworkFormatEnum FormatFor(string lessonId)
    {
        if (LoadFormats().TryGetValue(lessonId, out var format))
            return format;

        return LoadLessons().FirstOrDefault(lesson => lesson.Id == lessonId) is { } found ? SubjectFormatFor(found.Title) : HomeworkFormats.DEFAULT;
    }

    /// <summary>
    /// Файл с форматами по умолчанию для предметов
    /// </summary>
    private string SubjectFormatsFile => Path.Combine(directory, "subject-formats.json");

    /// <summary>
    /// Формат заданий по умолчанию для предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Выбранный формат или <see cref="HomeworkFormats.DEFAULT"/></returns>
    public HomeworkFormatEnum SubjectFormatFor(string subject) =>
        (Read<Dictionary<string, HomeworkFormatEnum>>(SubjectFormatsFile) ?? []).TryGetValue(subject, out var format) ? format : HomeworkFormats.DEFAULT;

    /// <summary>
    /// Запоминает формат заданий по умолчанию для предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="format">Выбранный формат</param>
    public void SetSubjectFormat(string subject, HomeworkFormatEnum format)
    {
        var formats = (Read<Dictionary<string, HomeworkFormatEnum>>(SubjectFormatsFile) ?? []).ToDictionary(pair => pair.Key, pair => pair.Value);
        formats[subject] = format;

        Directory.CreateDirectory(directory);

        var temp = SubjectFormatsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(formats, Json));
        File.Move(temp, SubjectFormatsFile, overwrite: true);
    }

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
    /// Файл с отметками, что делать с занятиями
    /// </summary>
    private string StatesFile => Path.Combine(directory, "lesson-states.json");

    /// <summary>
    /// Читает отметки занятий
    /// </summary>
    /// <returns>Отметка по ключу занятия. У занятий без выбора ключа нет</returns>
    public IReadOnlyDictionary<string, LessonStateEnum> LoadStates() =>
        Read<Dictionary<string, LessonStateEnum>>(StatesFile) ?? [];

    /// <summary>
    /// Отметка занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Выбранная отметка или <see cref="LessonStateEnum.Undecided"/></returns>
    public LessonStateEnum StateFor(string lessonId) =>
        LoadStates().TryGetValue(lessonId, out var state) ? state : LessonStateEnum.Undecided;

    /// <summary>
    /// Запоминает отметку занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="state">Что делать с занятием</param>
    public void SetState(string lessonId, LessonStateEnum state) => SetStates([new KeyValuePair<string, LessonStateEnum>(lessonId, state)]);

    /// <summary>
    /// Запоминает отметки нескольких занятий одной записью файла
    /// </summary>
    /// <param name="changes">Ключ занятия и новая отметка</param>
    public void SetStates(IEnumerable<KeyValuePair<string, LessonStateEnum>> changes)
    {
        var states = LoadStates().ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var (id, state) in changes)
            states[id] = state;

        Directory.CreateDirectory(directory);

        var temp = StatesFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(states, Json));
        File.Move(temp, StatesFile, overwrite: true);
    }

    /// <summary>
    /// Записывает список занятий. Занятия, которых нет в новом списке, остаются: расшифровка и задания по ним уже могут быть
    /// </summary>
    /// <param name="lessons">Занятия, найденные при последней выгрузке</param>
    public void SaveLessons(IEnumerable<Lesson> lessons)
    {
        var merged = LoadLessons().ToDictionary(lesson => lesson.Id, StringComparer.Ordinal);
        foreach (var lesson in lessons)
            merged[lesson.Id] = lesson;

        Directory.CreateDirectory(directory);

        var temp = LessonsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(merged.Values.OrderByDescending(lesson => lesson.Date).ThenBy(lesson => lesson.Id, StringComparer.Ordinal), Json));
        File.Move(temp, LessonsFile, overwrite: true);
    }

    /// <summary>
    /// Файл с отметками предметов: что делать с занятиями предмета
    /// </summary>
    private string SubjectStatesFile => Path.Combine(directory, "subject-states.json");

    /// <summary>
    /// Отметка предмета
    /// </summary>
    /// <param name="subject">Название предмета, как оно записано в занятиях</param>
    /// <returns>Выбранная отметка или <see cref="LessonStateEnum.Undecided"/></returns>
    public LessonStateEnum SubjectStateFor(string subject) =>
        (Read<Dictionary<string, LessonStateEnum>>(SubjectStatesFile) ?? []).TryGetValue(subject, out var state) ? state : LessonStateEnum.Undecided;

    /// <summary>
    /// Запоминает отметку предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="state">Что делать с занятиями предмета</param>
    public void SetSubjectState(string subject, LessonStateEnum state)
    {
        var states = (Read<Dictionary<string, LessonStateEnum>>(SubjectStatesFile) ?? []).ToDictionary(pair => pair.Key, pair => pair.Value);
        states[subject] = state;

        Directory.CreateDirectory(directory);

        var temp = SubjectStatesFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(states, Json));
        File.Move(temp, SubjectStatesFile, overwrite: true);
    }

    /// <summary>
    /// Решает, нужно ли забирать расшифровку занятия: текста ещё нет и либо занятие заказано отдельно (его отметка «забирать» сильнее отметки предмета), либо предмет отмечен «расшифровывать» и у занятия нет отметки «не нужно» или «завершено»
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если расшифровку пора забирать
    /// </returns>
    public bool ShouldFetch(Lesson lesson) =>
        lesson.RecordUrl.Length > 0
        && !HasTranscript(lesson.Id)
        && (StateFor(lesson.Id) == LessonStateEnum.Fetch || (SubjectStateFor(lesson.Title) == LessonStateEnum.Fetch && StateFor(lesson.Id) is not (LessonStateEnum.Skip or LessonStateEnum.Done)));

    /// <summary>
    /// Проверяет, лежит ли уже расшифровка занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>
    /// <see langword="true"/>, если файл расшифровки есть и не пуст
    /// </returns>
    public bool HasTranscript(string lessonId) => File.Exists(TranscriptPath(lessonId)) && new FileInfo(TranscriptPath(lessonId)).Length > 0;

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
    /// Читает текст расшифровки занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Текст или пустая строка, если расшифровки нет</returns>
    public string ReadTranscript(string lessonId)
    {
        try
        {
            return File.Exists(TranscriptPath(lessonId)) ? File.ReadAllText(TranscriptPath(lessonId)) : string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Готовые файлы с домашним заданием занятия: всё, что лежит в <c>drafts/&lt;ключ&gt;</c>
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Пути к файлам от новых к старым. Пустой список, если ничего не готовили</returns>
    public IReadOnlyList<string> DraftFiles(string lessonId)
    {
        var folder = Path.Combine(directory, "drafts", SafeName(lessonId));

        return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder).OrderByDescending(File.GetLastWriteTimeUtc)] : [];
    }

    /// <summary>
    /// Читает работы из LMS, которые выгрузка записала в <c>activities.json</c>
    /// </summary>
    /// <returns>Работы. Пустой список, если файла нет или он испорчен</returns>
    public IReadOnlyList<ActivityInfo> LoadActivities()
    {
        try
        {
            if (!File.Exists(Path.Combine(directory, "activities.json")))
                return [];

            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "activities.json")));

            return
            [
                .. document.RootElement.EnumerateArray().Select(item => new ActivityInfo(
                    item.TryGetProperty("subject", out var subject) ? subject.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.Number && kind.GetInt32() == 1,
                    item.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True))
            ];
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            return [];
        }
    }

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