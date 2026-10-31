using System.Text.Encodings.Web;
using System.Text.Json;
using Steward.Homework.Models;
using Steward.Homework.Services;

namespace Steward.Homework.Storage;

/// <summary>
/// Папка домашних заданий: список занятий (его пишет выгрузка из MTS Link) и выбранные форматы (их пишет приложение)
/// </summary>
/// <param name="directory">Папка, например <c>data/homework</c></param>
public sealed partial class HomeworkStore(string directory)
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
    /// <exception cref="IOException">Файл занят дольше ожидания или не читается: прежние ключи целы, новые не записаны</exception>
    public void MarkCreated(IEnumerable<string> externalIds)
    {
        var added = externalIds.ToList();
        Locked(() =>
        {
            var all = (JsonFiles.ReadForWrite<List<string>>(CreatedFile, Json) ?? []).Union(added).Order(StringComparer.Ordinal).ToList();
            WriteAtomic(CreatedFile, all);
        });
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
    /// Наибольший размер своей готовой работы, байты: 50 МБ
    /// </summary>
    public const long OWN_WORK_MAX_BYTES = 50L * 1024 * 1024;

    /// <summary>
    /// Наибольшая длина расширения своей работы вместе с точкой
    /// </summary>
    private const int OWN_WORK_EXTENSION_MAX = 11;

    /// <summary>
    /// Прикрепляет готовую работу владельца: копирует файл как <c>Задание N.&lt;расширение&gt;</c> в папку занятия, и шаг задания становится «Готово» без запуска агента. Копия пишется в <c>.partial</c> и переименовывается целиком, поэтому недописанный файл готовой работой не считается. Прежние файлы этого задания переносятся в <c>.previous</c> (с новым именем, если там такой файл уже есть), ничего не затирается. Тот же файл, прикреплённый повторно, ничего не меняет. Вызывающий обязан держать блокировку подготовки: прерванная фоновая попытка удалила бы новый файл
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="file">Путь к файлу владельца</param>
    /// <returns>Путь к файлу работы в папке занятия</returns>
    /// <exception cref="ArgumentException">Ключ занятия недопустим для имени папки</exception>
    /// <exception cref="InvalidDataException">Номер, файл или расширение не подходят; текст ошибки можно показать владельцу</exception>
    /// <exception cref="IOException">Файл не удалось скопировать или заменить прежний</exception>
    public string AttachOwnWork(string lessonId, int number, string file)
    {
        var folder = DraftsDirectory(lessonId);
        if (number < 1)
            throw new InvalidDataException("Неверный номер задания: он начинается с единицы");

        var info = OwnWorkSource(file);
        var extension = OwnWorkExtension(info.Extension);
        var name = $"Задание {number}{extension}";
        var target = Path.Combine(folder, name);
        if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(target)), Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Имя работы выходит за пределы папки занятия");

        var existing = DraftFiles(lessonId).Where(draft => IsTaskFile(draft, number)).ToList();
        if (existing.FirstOrDefault(draft => string.Equals(Path.GetExtension(draft), extension, StringComparison.OrdinalIgnoreCase) && (string.Equals(Path.GetFullPath(draft), info.FullName, StringComparison.OrdinalIgnoreCase) || SameContent(draft, info))) is { } same)
        {
            RememberAttached(lessonId, Path.GetFileName(same), []);

            return same;
        }

        Directory.CreateDirectory(folder);
        var partial = target + ".partial";
        try
        {
            CopyLimited(info.FullName, partial);
        }
        catch
        {
            DeleteQuietly(partial);

            throw;
        }

        var moved = new List<(string From, string To)>();
        try
        {
            var previous = Path.Combine(folder, PREVIOUS_FOLDER);
            foreach (var draft in existing)
            {
                Directory.CreateDirectory(previous);

                var destination = UniquePath(previous, Path.GetFileName(draft));
                File.Move(draft, destination);
                moved.Add((draft, destination));
            }

            File.Move(partial, target);
        }
        catch
        {
            foreach (var (from, to) in Enumerable.Reverse(moved))
            {
                try
                {
                    File.Move(to, from);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Прежний файл остаётся в .previous: он не потерян
                }
            }

            DeleteQuietly(partial);

            throw;
        }

        RememberAttached(lessonId, name, existing.Select(Path.GetFileName).OfType<string>());

        return target;
    }

    /// <summary>
    /// Папка прежних версий работ внутри папки занятия
    /// </summary>
    public const string PREVIOUS_FOLDER = ".previous";

    /// <summary>
    /// Папка внутри папки занятия, куда откладываются файлы неудачной или прерванной попытки агента: они не считаются готовой работой, но и не удаляются
    /// </summary>
    public const string FAILED_FOLDER = ".failed";

    /// <summary>
    /// Папка служебных отметок владельца внутри папки занятия: агенту закрыта, в готовые файлы не попадает
    /// </summary>
    public const string OWNER_FOLDER = ".owner";

    /// <summary>
    /// Отметка прикреплённых работ: имена файлов в папке занятия, которые прикрепил владелец. Лежит в <c>.owner</c>, поэтому сама готовой работой не считается
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Путь к <c>drafts/&lt;ключ&gt;/.owner/attached.json</c></returns>
    private string AttachedFile(string lessonId) => Path.Combine(DraftsDirectory(lessonId), OWNER_FOLDER, "attached.json");

    /// <summary>
    /// Имена файлов в папке занятия, которые прикрепил владелец: переделка их не трогает
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <returns>Имена файлов без учёта регистра; пусто, если отметки нет или она не читается</returns>
    public IReadOnlySet<string> AttachedWorks(string lessonId) =>
        (Read<List<string>>(AttachedFile(lessonId)) ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Дописывает прикреплённый файл в отметку и убирает из неё файлы, которые он заменил. Пишется атомарно; испорченная отметка откладывается в <c>.bad</c>. Вызывающий держит блокировку подготовки
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="name">Имя прикреплённого файла</param>
    /// <param name="replaced">Имена файлов, которые ушли в <c>.previous</c></param>
    /// <exception cref="IOException">Отметку не удалось прочитать или записать</exception>
    private void RememberAttached(string lessonId, string name, IEnumerable<string> replaced)
    {
        var path = AttachedFile(lessonId);
        var names = new SortedSet<string>(JsonFiles.ReadForWrite<List<string>>(path, Json) ?? [], StringComparer.OrdinalIgnoreCase);
        var removed = names.RemoveWhere(new HashSet<string>(replaced, StringComparer.OrdinalIgnoreCase).Contains) > 0;
        var added = names.Add(name);
        if (removed || added)
            WriteAtomic(path, names.ToList());
    }

    /// <summary>
    /// Переносит в <c>.previous</c> готовые файлы занятия, которые переделываются. Остаются на месте файлы прикреплённых владельцем работ (по отметке в <c>.owner</c>) и файлы заданий из <paramref name="kept"/> (сданные и в архиве): они остаются образцами и готовой работой. Если в <c>.previous</c> уже лежит файл с таким именем, новый получает имя « (2)» и так далее: прежние версии не затираются
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="kept">Номера заданий, которые не переделываются</param>
    /// <returns>Новые пути перенесённых файлов</returns>
    public IReadOnlyList<string> MoveDraftsToPrevious(string lessonId, IReadOnlySet<int> kept)
    {
        var moved = new List<string>();
        var previous = Path.Combine(DraftsDirectory(lessonId), PREVIOUS_FOLDER);
        var attached = AttachedWorks(lessonId);
        foreach (var file in DraftFiles(lessonId))
        {
            if (attached.Contains(Path.GetFileName(file)) || kept.Any(number => IsTaskFile(file, number)))
                continue;

            Directory.CreateDirectory(previous);

            var target = UniquePath(previous, Path.GetFileName(file));
            File.Move(file, target);
            moved.Add(target);
        }

        return moved;
    }

    /// <summary>
    /// Проверяет исходный файл своей работы: он есть, это файл (не папка и не ссылка) и не больше предела
    /// </summary>
    /// <param name="file">Путь к файлу владельца</param>
    /// <returns>Сведения о файле с полным путём</returns>
    /// <exception cref="InvalidDataException">Файл не подходит</exception>
    private static FileInfo OwnWorkSource(string file)
    {
        FileInfo info;
        try
        {
            if (string.IsNullOrWhiteSpace(file))
                throw new InvalidDataException("Файл не выбран");

            info = new FileInfo(Path.GetFullPath(file.Trim().Trim('"')));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("Путь к файлу недопустим");
        }

        if (Directory.Exists(info.FullName))
            throw new InvalidDataException("Это не файл, а папка: выбери файл работы");

        if (!info.Exists)
            throw new InvalidDataException("Файл не найден: " + info.FullName);

        // Заглушки OneDrive и Dropbox тоже помечены как точки повторной обработки, поэтому ссылкой считается только настоящая символьная ссылка или соединение
        if (info.LinkTarget is not null)
            throw new InvalidDataException("Это ссылка, а не сам файл: выбери файл работы");

        if (info.Length > OWN_WORK_MAX_BYTES)
            throw new InvalidDataException($"Файл больше {OWN_WORK_MAX_BYTES / 1024 / 1024} МБ: прикрепи работу поменьше");

        return info;
    }

    /// <summary>
    /// Приводит расширение к безопасному виду: нижний регистр, точка и до десяти латинских букв или цифр
    /// </summary>
    /// <param name="extension">Расширение файла с точкой</param>
    /// <returns>Расширение с точкой</returns>
    /// <exception cref="InvalidDataException">Расширения нет, оно длинное, содержит другие знаки или совпадает со служебным</exception>
    private static string OwnWorkExtension(string extension)
    {
        var lower = extension.ToLowerInvariant();
        if (lower.Length < 2 || lower.Length > OWN_WORK_EXTENSION_MAX || lower[0] != '.' || !lower.Skip(1).All(char.IsAsciiLetterOrDigit) || lower is ".tmp" or ".partial")
            throw new InvalidDataException("У файла нет допустимого расширения (латиница и цифры, до 10 знаков): переименуй его, например в .docx");

        return lower;
    }

    /// <summary>
    /// Копирует файл целиком во временный и проверяет, что он не вырос выше предела за время копирования
    /// </summary>
    /// <param name="source">Исходный файл</param>
    /// <param name="partial">Недописанный файл назначения</param>
    /// <exception cref="InvalidDataException">Файл вырос выше предела</exception>
    private static void CopyLimited(string source, string partial)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += read;

            // Файл мог вырасти после проверки размера: копирование останавливается, как только предел превышен
            if (total > OWN_WORK_MAX_BYTES)
                throw new InvalidDataException($"Файл больше {OWN_WORK_MAX_BYTES / 1024 / 1024} МБ: прикрепи работу поменьше");

            output.Write(buffer, 0, read);
        }
    }

    /// <summary>
    /// Совпадает ли содержимое готовой работы с выбранным файлом
    /// </summary>
    /// <param name="draft">Файл в папке занятия</param>
    /// <param name="source">Файл владельца</param>
    /// <returns>
    /// <see langword="true"/>, если размер и все байты совпадают; при ошибке чтения <see langword="false"/>
    /// </returns>
    private static bool SameContent(string draft, FileInfo source)
    {
        try
        {
            if (new FileInfo(draft).Length != source.Length)
                return false;

            using var left = new FileStream(draft, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var right = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            var first = new byte[81920];
            var second = new byte[81920];
            int read;
            while ((read = left.ReadAtLeast(first, first.Length, throwOnEndOfStream: false)) > 0)
            {
                if (right.ReadAtLeast(second, read, throwOnEndOfStream: false) < read || !first.AsSpan(0, read).SequenceEqual(second.AsSpan(0, read)))
                    return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Свободный путь в папке: если имя занято, добавляется « (2)», « (3)» и так далее перед расширением
    /// </summary>
    /// <param name="folder">Папка</param>
    /// <param name="name">Желаемое имя файла</param>
    /// <returns>Путь к несуществующему файлу</returns>
    public static string UniquePath(string folder, string name)
    {
        var path = Path.Combine(folder, name);
        for (var index = 2; File.Exists(path); index++)
            path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(name)} ({index}){Path.GetExtension(name)}");

        return path;
    }

    /// <summary>
    /// Удаляет файл, не падая, если он занят или его уже нет
    /// </summary>
    /// <param name="path">Файл</param>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Недописанный файл всё равно не считается готовой работой
        }
    }

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
    /// Выгружено ли условие задания из LMS: у задания есть номер работы и файл <c>assignment.md</c>
    /// </summary>
    /// <param name="item">Задание</param>
    /// <returns>
    /// <see langword="true"/>, если условие выгружено
    /// </returns>
    public bool HasAssignmentCondition(HomeworkItem item) =>
        item.ActivityId.Length > 0 && item.ActivityId.All(char.IsAsciiDigit) && File.Exists(Path.Combine(AssignmentDirectory(item.ActivityId), "assignment.md"));

    /// <summary>
    /// Номера работ LMS, которые LMS показывает сданными
    /// </summary>
    /// <returns>Номера работ из снимка <c>activities.json</c></returns>
    public IReadOnlySet<string> DoneWorkIds() =>
        LoadActivities().Where(work => work.Done && work.Id.Length > 0).Select(work => work.Id).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Номера заданий занятия, которые сданы по любому признаку: отметка в файле заданий, отметка владельца или работа сдана в LMS
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="items">Задания занятия</param>
    /// <param name="doneWorks">Номера сданных в LMS работ; <see langword="null"/>, чтобы прочитать их из снимка</param>
    /// <returns>Номера заданий с единицы</returns>
    public IReadOnlySet<int> SubmittedAnywhere(string lessonId, IReadOnlyList<HomeworkItem> items, IReadOnlySet<string>? doneWorks = null)
    {
        var submitted = SubmittedNumbers(lessonId);
        var done = doneWorks ?? DoneWorkIds();

        return Enumerable.Range(1, items.Count)
            .Where(number => items[number - 1].Done || submitted.Contains(number) || (items[number - 1].ActivityId.Length > 0 && done.Contains(items[number - 1].ActivityId)))
            .ToHashSet();
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
    /// <exception cref="IOException">Файл занят другим процессом дольше ожидания или не записался</exception>
    private void SetItemState(string lessonId, int number, string? state) => Locked(() =>
    {
        var states = new Dictionary<string, string>(JsonFiles.ReadForWrite<Dictionary<string, string>>(ItemStatesFile, Json) ?? [], StringComparer.Ordinal);
        if (state is not null)
            states[ItemKey(lessonId, number)] = state;
        else
            states.Remove(ItemKey(lessonId, number));

        WriteAtomic(ItemStatesFile, states);
    });

    /// <summary>
    /// Начало имени мьютекса для отметок владельца: сдано, архив, подтверждение, правка условия
    /// </summary>
    private const string LOCK_PREFIX = "Steward.HomeworkMarks.";

    /// <summary>
    /// Выполняет чтение и запись отметок владельца под общей блокировкой потоков и процессов: окно пишет их из фоновых задач, и записи не должны затирать друг друга
    /// </summary>
    /// <param name="action">Чтение, изменение и запись файла</param>
    /// <exception cref="IOException">Блокировка занята дольше ожидания</exception>
    private void Locked(Action action) => FileLock.Run(directory, LOCK_PREFIX, action);

    /// <summary>
    /// Пишет JSON атомарно: во временный файл с уникальным именем, затем переименованием поверх прежнего
    /// </summary>
    /// <param name="path">Файл</param>
    /// <param name="value">Содержимое</param>
    private static void WriteAtomic(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            DeleteQuietly(temp);

            throw;
        }
    }

    /// <summary>
    /// Нужно ли искать задания в расшифровке: расшифровка есть, файла заданий нет, занятие готовится как «Расшифровывать» не в формате «Не готовить», а задания предмета не лежат в LMS (их строит <see cref="SyncLmsItems"/> без агента)
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>
    /// <see langword="true"/>, если поиск заданий пора запускать
    /// </returns>
    public bool ShouldExtract(Lesson lesson) => HasTranscript(lesson.Id) && !File.Exists(ItemsPath(lesson.Id)) && PreparesHomework(lesson) && TaskSourceFor(lesson.Title) != TaskSourceEnum.Lms;

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
    /// <exception cref="IOException">Файл занят дольше ожидания или не записался</exception>
    public void Confirm(string lessonId, int number) => Locked(() => ConfirmCore(lessonId, number));

    /// <summary>
    /// Подтверждение без блокировки: вызывается под ней
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <exception cref="IOException">Файл подтверждений не читается или не записался: прежние подтверждения целы</exception>
    private void ConfirmCore(string lessonId, int number)
    {
        var keys = new HashSet<string>(JsonFiles.ReadForWrite<List<string>>(ItemConfirmedFile, Json) ?? [], StringComparer.Ordinal) { ItemKey(lessonId, number) };
        WriteAtomic(ItemConfirmedFile, keys.Order(StringComparer.Ordinal).ToList());
    }

    /// <summary>
    /// Записывает правку условия задания и подтверждает его
    /// </summary>
    /// <param name="lessonId">Ключ занятия</param>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="title">Верное название, пусто значит оставить прежнее</param>
    /// <param name="requirements">Верные требования</param>
    /// <exception cref="IOException">Файл занят дольше ожидания или не записался</exception>
    public void SetOverride(string lessonId, int number, string title, string requirements) => Locked(() =>
    {
        var overrides = new Dictionary<string, ItemOverride>(JsonFiles.ReadForWrite<Dictionary<string, ItemOverride>>(ItemOverridesFile, Json) ?? [], StringComparer.Ordinal)
        {
            [ItemKey(lessonId, number)] = new ItemOverride(title.Trim(), requirements.Trim()),
        };

        WriteAtomic(ItemOverridesFile, overrides);
        ConfirmCore(lessonId, number);
    });

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
    /// Что показывать на карточке о каждом задании занятия: шаг конвейера, файл работы, оценку уверенности, источник и подсказку. Оценка считается теми же входами, что и автоподготовка, поэтому вопрос на карточке совпадает с тем, почему задание не готовится. Отчёт о проверке один на занятие, поэтому «Проверено» бывает только у задания с работой
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Сведения по номеру задания с единицы; пусто, если задания ещё не искали или ключ занятия недопустим</returns>
    public IReadOnlyDictionary<int, ItemPipelineInfo> PipelineInfo(Lesson lesson)
    {
        try
        {
            if (ReadItems(lesson.Id) is not { Count: > 0 } items)
                return new Dictionary<int, ItemPipelineInfo>();

            var submitted = SubmittedAnywhere(lesson.Id, items);
            var confirmed = ConfirmedNumbers(lesson.Id);
            var hasTranscript = HasTranscript(lesson.Id);
            var transcript = hasTranscript ? ReadTranscript(lesson.Id) : string.Empty;
            var hasReport = File.Exists(CheckReportPath(lesson.Id));
            var prepares = PreparesHomework(lesson) && SubjectFormatFor(lesson.Title) != HomeworkFormatEnum.Skip && SubjectOptionsFor(lesson.Title).AutoPrepare;
            var attempts = new PrepareLedger(directory).AllAttempts();

            // Папка работ читается один раз на занятие: самый свежий файл задания тот же, что вернул бы SampleCandidate
            var drafts = DraftFiles(lesson.Id);
            var result = new Dictionary<int, ItemPipelineInfo>();
            for (var number = 1; number <= items.Count; number++)
            {
                var item = items[number - 1];
                var work = drafts.FirstOrDefault(file => IsTaskFile(file, number)) ?? string.Empty;
                var step = PipelineStepper.Of(submitted.Contains(number), work.Length > 0, work.Length > 0 && hasReport);
                var verified = EvidenceCheck.Verify(transcript, item.Evidence);
                var assessment = ItemAssessor.Assess(item, verified, hasTranscript, HasAssignmentCondition(item), confirmed.Contains(number));

                // Связь с LMS: номер работы LMS, как его понимает оценка уверенности. Просто ссылка в тексте работой LMS не считается
                var linked = item.ActivityId.Length > 0 && item.ActivityId.All(char.IsAsciiDigit);
                var (hint, problem) = step == PipelineStepEnum.Found && assessment.Readiness != ItemReadinessEnum.NeedsConfirmation
                    ? FoundHint(attempts.GetValueOrDefault($"{lesson.Id}:{number}"), prepares)
                    : (string.Empty, false);
                result[number] = new ItemPipelineInfo(step, work, assessment, verified, linked, hint, problem);
            }

            return result;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new Dictionary<int, ItemPipelineInfo>();
        }
    }

    /// <summary>
    /// Подсказка для задания без работы: что будет дальше или почему работа не готовится
    /// </summary>
    /// <param name="attempts">Сколько раз подряд агент не справился</param>
    /// <param name="prepares">Предмет и занятие готовятся сами: включена автоподготовка предмета, нужная отметка и формат</param>
    /// <returns>Текст подсказки и признак ошибки</returns>
    private static (string Hint, bool Problem) FoundHint(int attempts, bool prepares)
    {
        if (attempts >= HomeworkPreparer.MAX_ATTEMPTS)
            return ($"Агент не справился {HomeworkPreparer.MAX_ATTEMPTS} раза подряд: подготовь работу кнопкой «Подготовить задание»", true);

        if (!prepares)
            return ("Автоподготовка у предмета выключена: подготовь работу кнопкой «Подготовить задание»", false);

        return attempts > 0
            ? ("Прошлая попытка не удалась, повтор при ближайшей выгрузке", true)
            : ("Работа подготовится сама при ближайшей выгрузке", false);
    }

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