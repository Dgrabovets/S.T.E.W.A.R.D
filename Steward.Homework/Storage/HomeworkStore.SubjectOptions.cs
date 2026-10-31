using System.Globalization;
using System.Text.Json;
using Steward.Homework.Models;

namespace Steward.Homework.Storage;

/// <summary>
/// Настройки предмета для домашних заданий: готовить ли работы сами и откуда брать задания, а также карточки заданий, построенные по работам LMS без агента
/// </summary>
public sealed partial class HomeworkStore
{
    /// <summary>
    /// Сколько знаков должно быть в тексте условия работы LMS, чтобы считать, что условие там действительно написано, а не стоит одна заглушка
    /// </summary>
    private const int MIN_CONDITION_CHARS = 40;

    /// <summary>
    /// Строка-заглушка, которую выгрузка пишет, когда на странице работы нет текста условия
    /// </summary>
    private const string NO_CONDITION_STUB = "Текст условия на странице не указан";

    /// <summary>
    /// Файл с настройками предметов
    /// </summary>
    private string SubjectOptionsFile => Path.Combine(directory, "subject-options.json");

    /// <summary>
    /// Читает настройки предмета
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Настройки или значения по умолчанию, если для предмета ничего не записано или файл не читается</returns>
    public SubjectOptions SubjectOptionsFor(string subject) =>
        (Read<Dictionary<string, SubjectOptions>>(SubjectOptionsFile) ?? []).TryGetValue(subject, out var options) ? options : new SubjectOptions();

    /// <summary>
    /// Включает или выключает у предмета автоматическую подготовку работ фоновым процессом. Ручная кнопка «Подготовить задание» от этого не зависит
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="autoPrepare">Готовить сами</param>
    /// <exception cref="IOException">Файл настроек занят другим процессом дольше ожидания или не читается: прежнее содержимое цело</exception>
    public void SetSubjectAutoPrepare(string subject, bool autoPrepare) => ChangeSubjectOptions(subject, options => options with { AutoPrepare = autoPrepare });

    /// <summary>
    /// Запоминает, откуда у предмета берутся задания
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="source">Источник заданий</param>
    /// <exception cref="IOException">Файл настроек занят другим процессом дольше ожидания или не читается: прежнее содержимое цело</exception>
    public void SetSubjectTaskSource(string subject, TaskSourceEnum source) => ChangeSubjectOptions(subject, options => options with { TaskSource = source });

    /// <summary>
    /// Меняет настройки одного предмета под общей блокировкой, не затирая настройки остальных
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="change">Как изменить настройки предмета</param>
    private void ChangeSubjectOptions(string subject, Func<SubjectOptions, SubjectOptions> change) => Locked(() =>
    {
        var all = new Dictionary<string, SubjectOptions>(JsonFiles.ReadForWrite<Dictionary<string, SubjectOptions>>(SubjectOptionsFile, Json) ?? [], StringComparer.Ordinal);
        all[subject] = change(all.TryGetValue(subject, out var current) ? current : new SubjectOptions());
        WriteAtomic(SubjectOptionsFile, all);
    });

    /// <summary>
    /// Определяет, откуда берутся задания предмета: выбранный источник, а при «Определять самому» — LMS, если в нём у предмета есть несданная работа с текстом условия, иначе расшифровка
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>«В LMS» или «На занятии»: «Определять самому» здесь не возвращается</returns>
    public TaskSourceEnum TaskSourceFor(string subject)
    {
        var chosen = SubjectOptionsFor(subject).TaskSource;
        if (chosen != TaskSourceEnum.Auto)
            return chosen;

        return LoadActivities().Any(work => !work.IsQuiz && !work.Done && work.Id.Length > 0 && SameSubject(work.Subject, subject) && HasConditionText(work.Id))
            ? TaskSourceEnum.Lms
            : TaskSourceEnum.Transcript;
    }

    /// <summary>
    /// Проверяет, что у работы LMS в выгруженном описании есть настоящий текст условия, а не одна заглушка
    /// </summary>
    /// <param name="activityId">Номер работы в LMS</param>
    /// <returns>
    /// <see langword="true"/>, если текст условия написан
    /// </returns>
    private bool HasConditionText(string activityId)
    {
        var text = ConditionBody(activityId);

        return text.Length >= MIN_CONDITION_CHARS && !text.StartsWith(NO_CONDITION_STUB, StringComparison.Ordinal);
    }

    /// <summary>
    /// Достаёт из описания работы раздел «Условие»: текст до следующего раздела
    /// </summary>
    /// <param name="activityId">Номер работы в LMS</param>
    /// <returns>Текст условия без лишних пробелов или пустая строка, если описания нет или оно не читается</returns>
    private string ConditionBody(string activityId)
    {
        try
        {
            var file = Path.Combine(AssignmentDirectory(activityId), "assignment.md");
            if (!File.Exists(file))
                return string.Empty;

            var lines = File.ReadAllLines(file);
            var start = Array.FindIndex(lines, line => line.Trim() == "## Условие");
            if (start < 0)
                return string.Empty;

            var body = lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal));

            return string.Join(' ', body.Select(line => line.Trim()).Where(line => line.Length > 0));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Достаёт из описания работы ссылку на её страницу в LMS
    /// </summary>
    /// <param name="activityId">Номер работы в LMS</param>
    /// <returns>Ссылка или пустая строка</returns>
    private string LmsPage(string activityId)
    {
        const string PREFIX = "Страница в LMS:";
        try
        {
            var file = Path.Combine(AssignmentDirectory(activityId), "assignment.md");
            var line = File.Exists(file) ? File.ReadLines(file).FirstOrDefault(row => row.StartsWith(PREFIX, StringComparison.Ordinal)) : null;

            return line is null ? string.Empty : line[PREFIX.Length..].Trim();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Сравнивает названия предметов так же, как везде в программе: без учёта регистра
    /// </summary>
    /// <param name="left">Первое название</param>
    /// <param name="right">Второе название</param>
    /// <returns>
    /// <see langword="true"/>, если названия совпадают
    /// </returns>
    private static bool SameSubject(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Строит карточки заданий предметов, у которых задания лежат в LMS, прямо по работам LMS, без обращения к агенту: каждая несданная работа, которой ещё нет ни в одном занятии предмета, дописывается в конец файла заданий самого свежего занятия предмета с расшифровкой. Уже стоящие в файле задания не меняются, их номера остаются прежними
    /// </summary>
    /// <param name="lessons">Занятия, среди которых выбираются предметы и самое свежее занятие каждого</param>
    /// <returns>Сколько заданий добавлено</returns>
    /// <exception cref="IOException">Файл заданий не записался</exception>
    public int SyncLmsItems(IReadOnlyList<Lesson> lessons)
    {
        var works = LoadActivities().Where(work => !work.IsQuiz && work.Id.Length > 0).ToArray();
        var added = 0;
        foreach (var subject in lessons.Select(lesson => lesson.Title).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (TaskSourceFor(subject) != TaskSourceEnum.Lms)
                continue;

            var ofSubject = lessons.Where(lesson => SameSubject(lesson.Title, subject)).ToArray();
            var target = ofSubject.Where(lesson => PreparesHomework(lesson) && HasTranscript(lesson.Id)).OrderByDescending(lesson => lesson.Date).FirstOrDefault();
            if (target is null)
                continue;

            var listed = ofSubject.SelectMany(lesson => ReadItemsFile(lesson.Id)).Select(item => item.ActivityId).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
            var fresh = works.Where(work => SameSubject(work.Subject, subject) && !work.Done && !listed.Contains(work.Id)).ToArray();
            if (fresh.Length == 0)
                continue;

            var items = ReadItemsFile(target.Id).Concat(fresh.Select(work => new HomeworkItem(work.Name, work.Due, string.Empty, LmsPage(work.Id), ActivityId: work.Id))).ToArray();
            WriteItemsFile(target.Id, items);
            added += fresh.Length;
        }

        return added;
    }

    /// <summary>
    /// Читает файл заданий занятия как есть, без правок владельца
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Задания или пустой список, если файла нет или он не читается</returns>
    private IReadOnlyList<HomeworkItem> ReadItemsFile(string lessonId)
    {
        try
        {
            return File.Exists(ItemsPath(lessonId)) ? Services.HomeworkItemParser.Parse(File.ReadAllText(ItemsPath(lessonId))) : [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Записывает файл заданий занятия атомарно, в том же формате, который читает разбор ответа агента
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="items">Задания по порядку</param>
    private void WriteItemsFile(string lessonId, IReadOnlyList<HomeworkItem> items) => WriteAtomic(ItemsPath(lessonId), items.Select(item => new Dictionary<string, object?>
    {
        ["title"] = item.Title,
        ["due"] = item.Due?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        ["requirements"] = item.Requirements,
        ["link"] = item.Link,
        ["done"] = item.Done,
        ["activity"] = item.ActivityId,
        ["evidence"] = item.Evidence,
        ["confidence"] = item.Confidence,
        ["doubt"] = item.Doubt,
    }).ToList());
}
