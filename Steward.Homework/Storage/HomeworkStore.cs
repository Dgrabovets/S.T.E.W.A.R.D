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
    /// Код вида работы «тест» в файле <c>activities.json</c>: так его пишет выгрузка LMS
    /// </summary>
    private const int QUIZ_KIND = 1;

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
        && (StateFor(lesson.Id) is LessonStateEnum.Fetch or LessonStateEnum.Analyze || (SubjectStateFor(lesson.Title) is LessonStateEnum.Fetch or LessonStateEnum.Analyze && StateFor(lesson.Id) is not (LessonStateEnum.Skip or LessonStateEnum.Done)));

    /// <summary>
    /// Действующая отметка занятия: своя, а если её нет, отметка предмета. Отметки «Не нужно» и «Завершён» у занятия сильнее отметки предмета
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Отметка, по которой решается, что делать с занятием</returns>
    public LessonStateEnum EffectiveState(Lesson lesson)
    {
        var own = StateFor(lesson.Id);

        return own is LessonStateEnum.Fetch or LessonStateEnum.Analyze or LessonStateEnum.Skip or LessonStateEnum.Done ? own : SubjectStateFor(lesson.Title);
    }

    /// <summary>
    /// Нужно ли разобрать занятие: расшифровка есть, разбора ещё нет, а отметка требует разбора
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если разбор пора готовить
    /// </returns>
    public bool ShouldAnalyze(Lesson lesson) =>
        HasTranscript(lesson.Id) && !File.Exists(AnalysisPath(lesson.Id)) && EffectiveState(lesson) is LessonStateEnum.Fetch or LessonStateEnum.Analyze;

    /// <summary>
    /// Нужно ли готовить домашние задания по занятию: отметка «Расшифровывать» и формат не «Не готовить»
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если задания готовятся
    /// </returns>
    public bool PreparesHomework(Lesson lesson) => EffectiveState(lesson) == LessonStateEnum.Fetch && FormatFor(lesson.Id) != HomeworkFormatEnum.Skip;

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
                var archived = ArchivedNumbers(lesson.Id);

                return items.Select((item, index) => new HomeworkRow(lesson, FormatFor(lesson.Id), item, index + 1, created.Contains(HomeworkTaskBuilder.ExternalId(lesson, index + 1)))).Where(row => !archived.Contains(row.Number));
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
        var folder = DraftsDirectory(lessonId);

        return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder).Where(file => !Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal) && !file.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)).OrderByDescending(File.GetLastWriteTimeUtc)] : [];
    }

    /// <summary>
    /// Готовый файл задания: <c>Задание N.*</c> (так называет файл владелец или агент в формате кода) или <c>Задание N. Название.*</c> (так собирает работу Steward) в папке готовых файлов занятия. Недописанные (<c>.partial</c>, <c>.tmp</c>, <c>~$</c>) не считаются. По нему же решается, готова ли работа и что взять образцом
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Путь к самому свежему файлу или <see langword="null"/>, если файла нет или ключ занятия недопустим</returns>
    public string? SampleCandidate(string lessonId, int number)
    {
        try
        {
            return DraftFiles(lessonId).FirstOrDefault(file => IsTaskFile(file, number));
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Относится ли файл к заданию: имя начинается с «Задание N.» — это «Задание N.docx», «Задание N.tar.gz» и «Задание N. Название.docx», но не «Задание N1.docx»
    /// </summary>
    /// <param name="file">Путь или имя файла</param>
    /// <param name="number">Номер задания, с единицы</param>
    /// <returns>
    /// <see langword="true"/>, если файл этого задания
    /// </returns>
    public static bool IsTaskFile(string file, int number) =>
        Path.GetFileName(file).StartsWith($"Задание {number}.", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Папка с описанием и файлами задания из LMS
    /// </summary>
    /// <param name="activityId">Номер работы в LMS</param>
    /// <returns>Путь к <c>assignments/&lt;номер&gt;</c></returns>
    /// <exception cref="ArgumentException">Номер не подходит для имени папки</exception>
    public string AssignmentDirectory(string activityId) => Path.Combine(directory, "assignments", SafeName(activityId));

    /// <summary>
    /// Описания и файлы несданных заданий предмета из LMS: <c>assignment.md</c> и читаемые вложения каждого
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <returns>Пути к файлам. Пустой список, если описания ещё не выгружены</returns>
    public IReadOnlyList<string> AssignmentFiles(string subject)
    {
        var files = new List<string>();
        foreach (var work in LoadActivities().Where(work => !work.IsQuiz && !work.Done && work.Id.Length > 0 && string.Equals(work.Subject, subject, StringComparison.OrdinalIgnoreCase)))
        {
            string folder;
            try
            {
                folder = AssignmentDirectory(work.Id);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!Directory.Exists(folder))
                continue;

            files.AddRange(Directory.EnumerateFiles(folder).Where(file => !file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) && !file.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal));
        }

        return files;
    }

    /// <summary>
    /// Файл с отметками найденных заданий: «archived» для убранных в архив
    /// </summary>
    private string ItemStatesFile => Path.Combine(directory, "item-states.json");

    /// <summary>
    /// Ключ найденного задания в файле отметок
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Строка вида «занятие:номер»</returns>
    private static string ItemKey(string lessonId, int number) => $"{lessonId}:{number}";

    /// <summary>
    /// Задание убрано в архив: оно не показывается в списке и не готовится
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>
    /// <see langword="true"/>, если задание в архиве
    /// </returns>
    public bool IsArchived(string lessonId, int number) =>
        (Read<Dictionary<string, string>>(ItemStatesFile) ?? []).TryGetValue(ItemKey(lessonId, number), out var state) && state == "archived";

    /// <summary>
    /// Ближайшие сроки несданных работ: из LMS и из найденных в расшифровках заданий. Сданные, отмеченные вручную и убранные в архив не попадают
    /// </summary>
    /// <param name="today">Сегодняшняя дата</param>
    /// <param name="aheadDays">На сколько дней вперёд смотреть</param>
    /// <param name="overdueDays">Сколько дней после срока ещё напоминать</param>
    /// <returns>Сроки по возрастанию даты</returns>
    public IReadOnlyList<Deadline> Deadlines(DateOnly today, int aheadDays, int overdueDays)
    {
        var items = new List<(string Subject, HomeworkItem Item)>();
        foreach (var lesson in LoadLessons())
        {
            var skipped = ArchivedNumbers(lesson.Id).Union(SubmittedNumbers(lesson.Id)).ToHashSet();
            var found = ReadItems(lesson.Id) ?? [];
            items.AddRange(found.Where((_, index) => !skipped.Contains(index + 1)).Select(item => (lesson.Title, item)));
        }

        return DeadlineBuilder.Build(LoadActivities(), items, today, aheadDays, overdueDays);
    }

    /// <summary>
    /// Путь к отчёту о проверке готовой работы. Он лежит в служебной папке, поэтому не попадает в список готовых файлов
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>drafts/&lt;ключ&gt;/.check/report.md</c></returns>
    public string CheckReportPath(string lessonId) => Path.Combine(DraftsDirectory(lessonId), ".check", "report.md");

    /// <summary>
    /// Дата, когда программа заметила правку условия работы в LMS
    /// </summary>
    /// <param name="activityId">Номер работы в LMS</param>
    /// <returns>Дата или <see langword="null"/>, если правок не было</returns>
    public DateOnly? AssignmentChangedOn(string activityId)
    {
        if (activityId.Length == 0 || !activityId.All(char.IsAsciiDigit))
            return null;

        try
        {
            var file = Path.Combine(AssignmentDirectory(activityId), "changed.txt");

            return File.Exists(file) && DateOnly.TryParseExact(File.ReadAllText(file).Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Текст условия задания: название, срок и требования из расшифровки, а ниже условие из LMS, если оно выгружено
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns>Текст или пустая строка, если такого задания нет</returns>
    public string Condition(string lessonId, int number)
    {
        if (ReadItems(lessonId) is not { } items || number < 1 || number > items.Count)
            return string.Empty;

        var item = items[number - 1];
        var text = new System.Text.StringBuilder(item.Title);
        if (item.Due is { } due)
            text.AppendLine().Append("Срок: ").Append(due.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture));

        if (item.Requirements.Length > 0)
            text.AppendLine().Append("Требования: ").Append(item.Requirements);

        if (item.ActivityId.Length > 0 && item.ActivityId.All(char.IsAsciiDigit))
        {
            var file = Path.Combine(AssignmentDirectory(item.ActivityId), "assignment.md");
            try
            {
                if (File.Exists(file))
                    text.AppendLine().AppendLine().Append("Условие из LMS:").AppendLine().Append(File.ReadAllText(file));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Условие из LMS не читается: остаётся текст из расшифровки
            }
        }

        return text.ToString().Trim();
    }

    /// <summary>
    /// Номера заданий занятия, которые студент отметил сданными сам
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Номера заданий с единицы</returns>
    public IReadOnlySet<int> SubmittedNumbers(string lessonId) => NumbersWithState(lessonId, "submitted");

    /// <summary>
    /// Отмечает задание сданным вручную или снимает отметку. Задание в архиве не трогается
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="submitted">Сдано</param>
    public void SetSubmitted(string lessonId, int number, bool submitted) => SetItemState(lessonId, number, submitted ? "submitted" : null);

    /// <summary>
    /// Номера заданий занятия, убранных в архив. Файл отметок читается один раз на весь список
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Номера заданий с единицы</returns>
    public IReadOnlySet<int> ArchivedNumbers(string lessonId)
    {
        return NumbersWithState(lessonId, "archived");
    }

    /// <summary>
    /// Номера заданий занятия с заданной отметкой из файла отметок
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="state">Отметка: «archived» или «submitted»</param>
    /// <returns>Номера заданий с единицы</returns>
    private IReadOnlySet<int> NumbersWithState(string lessonId, string state)
    {
        var prefix = lessonId + ":";

        return (Read<Dictionary<string, string>>(ItemStatesFile) ?? [])
            .Where(pair => pair.Value == state && pair.Key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(pair.Key.AsSpan(prefix.Length), out _))
            .Select(pair => int.Parse(pair.Key.AsSpan(prefix.Length)))
            .ToHashSet();
    }

    /// <summary>
    /// Убирает задание в архив или возвращает его
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="archived">В архив</param>
    public void SetArchived(string lessonId, int number, bool archived) => SetItemState(lessonId, number, archived ? "archived" : null);

    /// <summary>
    /// Записывает отметку задания в файл отметок или убирает её
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="state">Отметка или <see langword="null"/>, чтобы убрать</param>
    private void SetItemState(string lessonId, int number, string? state)
    {
        var states = new Dictionary<string, string>(Read<Dictionary<string, string>>(ItemStatesFile) ?? [], StringComparer.Ordinal);
        if (state is not null)
            states[ItemKey(lessonId, number)] = state;
        else
            states.Remove(ItemKey(lessonId, number));

        Directory.CreateDirectory(directory);

        var temp = ItemStatesFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(states, Json));
        File.Move(temp, ItemStatesFile, overwrite: true);
    }

    /// <summary>
    /// Нужно ли искать задания в расшифровке: расшифровка есть, файла заданий нет, а занятие готовится как «Расшифровывать» не в формате «Не готовить»
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если поиск заданий пора запускать
    /// </returns>
    public bool ShouldExtract(Lesson lesson) => HasTranscript(lesson.Id) && !File.Exists(ItemsPath(lesson.Id)) && PreparesHomework(lesson);

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
                    item.TryGetProperty("kind", out var kind) && kind.ValueKind == JsonValueKind.Number && kind.GetInt32() == QUIZ_KIND,
                    item.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True,
                    item.TryGetProperty("section", out var section) ? section.GetString() ?? string.Empty : string.Empty,
                    item.TryGetProperty("due", out var due) && due.ValueKind == JsonValueKind.String && DateTime.TryParse(due.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dueDate) ? DateOnly.FromDateTime(dueDate) : null,
                    item.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty))
            ];
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>
    /// Путь к разбору занятия, который записывает агент
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>analysis/&lt;ключ&gt;.json</c></returns>
    /// <exception cref="ArgumentException">Ключ не подходит для имени файла</exception>
    public string AnalysisPath(string lessonId) => Path.Combine(directory, "analysis", SafeName(lessonId) + ".json");

    /// <summary>
    /// Читает разбор занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Разбор или <see langword="null"/>, если его ещё нет или файл испорчен</returns>
    public LessonAnalysis? ReadAnalysis(string lessonId)
    {
        try
        {
            return File.Exists(AnalysisPath(lessonId)) ? LessonAnalysis.Parse(File.ReadAllText(AnalysisPath(lessonId))) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Папка готовых файлов занятия
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>drafts/&lt;ключ&gt;</c></returns>
    /// <exception cref="ArgumentException">Ключ не подходит для имени файла</exception>
    public string DraftsDirectory(string lessonId) => Path.Combine(directory, "drafts", SafeName(lessonId));

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
    public IReadOnlyList<HomeworkItem>? ReadItems(string lessonId)
    {
        if (!File.Exists(ItemsPath(lessonId)))
            return null;

        var items = HomeworkItemParser.Parse(File.ReadAllText(ItemsPath(lessonId)));
        var overrides = Read<Dictionary<string, ItemOverride>>(ItemOverridesFile);
        if (overrides is not { Count: > 0 })
            return items;

        return [.. items.Select((item, index) => overrides.TryGetValue(ItemKey(lessonId, index + 1), out var fix)
            ? item with { Title = fix.Title.Length > 0 ? fix.Title : item.Title, Requirements = fix.Requirements.Length > 0 ? fix.Requirements : item.Requirements }
            : item)];
    }

    /// <summary>
    /// Файл с правками условий заданий, которые внёс владелец
    /// </summary>
    private string ItemOverridesFile => Path.Combine(directory, "item-overrides.json");

    /// <summary>
    /// Правка условия задания владельцем
    /// </summary>
    /// <param name="Title">Название, пустое значит «не менять»</param>
    /// <param name="Requirements">Требования, пустое значение значит «не менять»</param>
    private sealed record ItemOverride(string Title = "", string Requirements = "");

    /// <summary>
    /// Номера заданий занятия, которые владелец подтвердил
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Номера заданий с единицы</returns>
    public IReadOnlySet<int> ConfirmedNumbers(string lessonId)
    {
        var prefix = lessonId + ":";

        return (Read<List<string>>(ItemConfirmedFile) ?? [])
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(key.AsSpan(prefix.Length), out _))
            .Select(key => int.Parse(key.AsSpan(prefix.Length)))
            .ToHashSet();
    }

    /// <summary>
    /// Файл с ключами подтверждённых заданий: он отдельный, чтобы отметки «сдано» и «в архиве» не стирали подтверждение
    /// </summary>
    private string ItemConfirmedFile => Path.Combine(directory, "item-confirmed.json");

    /// <summary>
    /// Подтверждает задание: дальше оно идёт по конвейеру само
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    public void Confirm(string lessonId, int number)
    {
        var keys = new HashSet<string>(Read<List<string>>(ItemConfirmedFile) ?? [], StringComparer.Ordinal) { ItemKey(lessonId, number) };

        Directory.CreateDirectory(directory);

        var temp = ItemConfirmedFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(keys.Order(StringComparer.Ordinal), Json));
        File.Move(temp, ItemConfirmedFile, overwrite: true);
    }

    /// <summary>
    /// Записывает правку условия задания и подтверждает его
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="title">Верное название, пусто значит оставить прежнее</param>
    /// <param name="requirements">Верные требования</param>
    public void SetOverride(string lessonId, int number, string title, string requirements)
    {
        var overrides = new Dictionary<string, ItemOverride>(Read<Dictionary<string, ItemOverride>>(ItemOverridesFile) ?? [], StringComparer.Ordinal)
        {
            [ItemKey(lessonId, number)] = new ItemOverride(title.Trim(), requirements.Trim()),
        };

        Directory.CreateDirectory(directory);

        var temp = ItemOverridesFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(overrides, Json));
        File.Move(temp, ItemOverridesFile, overwrite: true);
        Confirm(lessonId, number);
    }

    /// <summary>
    /// Находится ли цитата задания в расшифровке занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <returns><see langword="true"/>, если цитата непустая и найдена в расшифровке</returns>
    public bool EvidenceVerified(Lesson lesson, int number) =>
        ReadItems(lesson.Id) is { } items && number >= 1 && number <= items.Count
        && EvidenceCheck.Verify(ReadTranscript(lesson.Id), items[number - 1].Evidence);

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