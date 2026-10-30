using System.IO;
using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Models;
using Steward.Mvvm;
using Steward.Preview;

namespace Steward.ViewModels;

/// <summary>
/// Список занятий с выбором формата ДЗ. Один на всё приложение: его показывают и окно, и страница, поэтому подготовка заданий, которая идёт в фоне, не сбрасывается при обновлении списка
/// </summary>
public sealed class HomeworkFormatsViewModel : Bindable
{
    /// <summary>
    /// Вызывается при выборе формата: параметры — ключ занятия и формат
    /// </summary>
    private readonly Action<string, Homework.Models.HomeworkFormatEnum> _chosen;

    /// <summary>
    /// Проверяет, есть ли у занятия расшифровка
    /// </summary>
    private readonly Func<Lesson, bool> _hasTranscript;

    /// <summary>
    /// Запоминает отметку предмета. Пусто: выбор отметок не показывается
    /// </summary>
    private readonly Action<string, LessonStateEnum>? _stateChosen;

    /// <summary>
    /// Просит забрать расшифровку занятия независимо от отметки предмета. Пусто: кнопки «Расшифровать» нет
    /// </summary>
    private readonly Action<Lesson>? _fetchRequested;

    /// <summary>
    /// Запоминает формат заданий по умолчанию для предмета. Пусто: выбор формата в карточке не показывается
    /// </summary>
    private readonly Action<string, HomeworkFormatEnum>? _subjectFormatChosen;

    /// <summary>
    /// Читает текущую отметку предмета по названию
    /// </summary>
    private readonly Func<string, LessonStateEnum>? _stateOf;

    /// <summary>
    /// Ищет задания в расшифровке занятия
    /// </summary>
    private readonly Func<Lesson, PrepareRequest, Task<(int Count, string Error)>> _prepare;

    /// <summary>
    /// Строки занятий
    /// </summary>
    private IReadOnlyList<LessonFormatViewModel> _lessons = [];

    /// <summary>
    /// Карточки предметов
    /// </summary>
    private IReadOnlyList<SubjectViewModel> _subjects = [];

    /// <summary>
    /// Предмет, панель которого открыта
    /// </summary>
    private SubjectViewModel? _selectedSubject;

    /// <summary>
    /// Формат заданий по умолчанию для каждого предмета, по названию
    /// </summary>
    private IReadOnlyDictionary<string, HomeworkFormatEnum> _formatByTitle = new Dictionary<string, HomeworkFormatEnum>();

    /// <summary>
    /// Открыта вкладка «Домашние задания»
    /// </summary>
    private bool _homeworkTab;

    /// <summary>
    /// Занятие, расшифровка которого открыта
    /// </summary>
    private LessonFormatViewModel? _openLesson;

    /// <summary>
    /// Разбор открытой расшифровки: тема, содержание, ключевые моменты
    /// </summary>
    private TranscriptText _reading = TranscriptText.Empty;

    /// <summary>
    /// Работы из LMS
    /// </summary>
    private IReadOnlyList<ActivityInfo> _activities = [];

    /// <summary>
    /// Задания, найденные в расшифровках
    /// </summary>
    private IReadOnlyList<HomeworkCardViewModel> _homeworkCards = [];

    /// <summary>
    /// Сколько методических указаний у выбранного предмета
    /// </summary>
    private int _guideCount;

    /// <summary>
    /// Сколько образцов у выбранного предмета
    /// </summary>
    private int _exampleCount;

    /// <summary>
    /// Создаёт модель без занятий
    /// </summary>
    /// <param name="chosen">Вызывается при выборе формата: параметры — ключ занятия и формат</param>
    /// <param name="hasTranscript">Проверяет, есть ли у занятия расшифровка</param>
    /// <param name="prepare">Ищет задания в расшифровке занятия: возвращает их число и текст ошибки</param>
    /// <param name="stateChosen">Запоминает отметку предмета: ключ — название предмета. Пусто: выбор отметок не показывается</param>
    /// <param name="stateOf">Читает отметку предмета по названию</param>
    /// <param name="fetchRequested">Просит забрать расшифровку занятия независимо от отметки предмета. Пусто: кнопки «Расшифровать» нет</param>
    /// <param name="subjectFormatChosen">Запоминает формат заданий по умолчанию для предмета. Пусто: выбор формата в карточке не показывается</param>
    public HomeworkFormatsViewModel(
        Action<string, HomeworkFormatEnum> chosen,
        Func<Lesson, bool> hasTranscript,
        Func<Lesson, PrepareRequest, Task<(int Count, string Error)>> prepare,
        Action<string, LessonStateEnum>? stateChosen = null,
        Func<string, LessonStateEnum>? stateOf = null,
        Action<Lesson>? fetchRequested = null,
        Action<string, HomeworkFormatEnum>? subjectFormatChosen = null)
    {
        ManageGuidesCommand = new RelayCommand(() => GuidesDialogRequested?.Invoke());
        ShowTranscriptsCommand = new RelayCommand(() => HomeworkTab = false);
        ShowHomeworkCommand = new RelayCommand(() => HomeworkTab = true);
        CloseLessonCommand = new RelayCommand(CloseLesson);
        CopyBriefCommand = new RelayCommand(CopyBrief);
        _fetchRequested = fetchRequested;
        _subjectFormatChosen = subjectFormatChosen;
        _stateChosen = stateChosen;
        _stateOf = stateOf;
        _chosen = chosen;
        _hasTranscript = hasTranscript;
        _prepare = prepare;
    }

    /// <summary>
    /// Создаёт модель сразу с занятиями
    /// </summary>
    /// <param name="lessons">Занятия с выбранными форматами</param>
    /// <param name="chosen">Вызывается при выборе формата: параметры — ключ занятия и формат</param>
    /// <param name="hasTranscript">Проверяет, есть ли у занятия расшифровка</param>
    /// <param name="prepare">Ищет задания в расшифровке занятия: возвращает их число и текст ошибки</param>
    /// <param name="stateChosen">Запоминает отметку предмета: ключ — название предмета. Пусто: выбор отметок не показывается</param>
    /// <param name="stateOf">Читает отметку предмета по названию</param>
    /// <param name="fetchRequested">Просит забрать расшифровку занятия независимо от отметки предмета. Пусто: кнопки «Расшифровать» нет</param>
    /// <param name="subjectFormatChosen">Запоминает формат заданий по умолчанию для предмета. Пусто: выбор формата в карточке не показывается</param>
    public HomeworkFormatsViewModel(
        IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons,
        Action<string, HomeworkFormatEnum> chosen,
        Func<Lesson, bool> hasTranscript,
        Func<Lesson, PrepareRequest, Task<(int Count, string Error)>> prepare,
        Action<string, LessonStateEnum>? stateChosen = null,
        Func<string, LessonStateEnum>? stateOf = null,
        Action<Lesson>? fetchRequested = null,
        Action<string, HomeworkFormatEnum>? subjectFormatChosen = null)
        : this(chosen, hasTranscript, prepare, stateChosen, stateOf, fetchRequested, subjectFormatChosen) => Update(lessons);

    /// <summary>
    /// Строки занятий
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> Lessons
    {
        get => _lessons;
        private set
        {
            if (!Set(ref _lessons, value))
                return;

            Raise(nameof(IsEmpty));
        }
    }

    /// <summary>
    /// Карточки предметов: в них те же занятия, но по одной отметке на предмет
    /// </summary>
    public IReadOnlyList<SubjectViewModel> Subjects
    {
        get => _subjects;
        private set => Set(ref _subjects, value);
    }

    /// <summary>
    /// Предмет, панель которого открыта под плитками
    /// </summary>
    public SubjectViewModel? SelectedSubject
    {
        get => _selectedSubject;
        private set => Set(ref _selectedSubject, value);
    }

    /// <summary>
    /// Занятий пока нет
    /// </summary>
    public bool IsEmpty => Lessons.Count == 0;

    /// <summary>
    /// Читает текст расшифровки по ключу занятия
    /// </summary>
    public Func<string, string>? TranscriptReader { get; set; }

    /// <summary>
    /// Читает разбор занятия по ключу: подробный, который записал Claude. Пусто, если разбора ещё нет
    /// </summary>
    public Func<string, LessonAnalysis?>? AnalysisReader { get; set; }

    /// <summary>
    /// Папка материалов приложения: по ней список понимает, что файл скопирован
    /// </summary>
    public string GuideFolder { get; set; } = string.Empty;

    /// <summary>
    /// Читает материалы предмета
    /// </summary>
    public Func<string, IReadOnlyList<GuideEntry>>? GuideLister { get; set; }

    /// <summary>
    /// Копирует выбранные файлы в материалы предмета и возвращает текст ошибки или пустую строку
    /// </summary>
    public Func<string, GuideKindEnum, IReadOnlyList<string>, string>? GuideFilesAdded { get; set; }

    /// <summary>
    /// Запоминает папку по пути на диске и возвращает текст ошибки или пустую строку
    /// </summary>
    public Func<string, GuideKindEnum, string, string>? GuideFolderAdded { get; set; }

    /// <summary>
    /// Запоминает ссылку в интернете, скачивает файл и возвращает пояснение или пустую строку
    /// </summary>
    public Func<string, GuideKindEnum, string, Task<string>>? GuideLinkAdded { get; set; }

    /// <summary>
    /// Сохраняет галочку «учитывать» у материала
    /// </summary>
    public Action<string, bool>? GuideEnabledChanged { get; set; }

    /// <summary>
    /// Убирает материал по ключу
    /// </summary>
    public Action<string>? GuideRemoved { get; set; }

    /// <summary>
    /// Открывает файл или папку в программе по умолчанию
    /// </summary>
    public Action<string>? PathOpened { get; set; }

    /// <summary>
    /// Сводка для строки над занятиями: сколько указаний и образцов у выбранного предмета
    /// </summary>
    public string GuideSummary => $"Указаний: {_guideCount}  ·  образцов: {_exampleCount}";

    /// <summary>
    /// Плашка «Указаний: 2» для строки над занятиями
    /// </summary>
    public string GuideCountText => $"Указаний: {_guideCount}";

    /// <summary>
    /// Плашка «Образцов: 1» для строки над занятиями
    /// </summary>
    public string ExampleCountText => $"Образцов: {_exampleCount}";

    /// <summary>
    /// Создаёт окно «Методические указания» для выбранного предмета
    /// </summary>
    /// <returns>Модель окна</returns>
    public GuidesDialogViewModel CreateGuidesDialog() => new(
        string.Empty,
        subject => GuideLister?.Invoke(subject) ?? [],
        GuideFolder,
        path => PathOpened?.Invoke(path),
        id => GuideRemoved?.Invoke(id),
        (id, enabled) => GuideEnabledChanged?.Invoke(id, enabled),
        (subject, kind, paths) => GuideFilesAdded?.Invoke(subject, kind, paths) ?? string.Empty,
        (subject, kind, path) => GuideFolderAdded?.Invoke(subject, kind, path) ?? string.Empty,
        (subject, kind, link) => GuideLinkAdded?.Invoke(subject, kind, link) ?? Task.FromResult(string.Empty),
        RemarkLister is { } load && RemarkUpdater is { } update && RemarkRemover is { } remove ? new RemarkHooks(load, update, remove) : null);

    /// <summary>
    /// Создаёт окно «Подготовить задание» для занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Модель окна</returns>
    public PrepareDialogViewModel CreatePrepareDialog(LessonFormatViewModel lesson) => new(lesson.Meta, lesson.Format, GuideLister?.Invoke(lesson.Title) ?? [], GuideFolder);

    /// <summary>
    /// Пересчитывает сводку по материалам выбранного предмета
    /// </summary>
    public void RefreshGuides()
    {
        var entries = GuideLister?.Invoke(string.Empty) ?? [];
        _guideCount = entries.Count(entry => entry.Kind == GuideKindEnum.Guide);
        _exampleCount = entries.Count(entry => entry.Kind == GuideKindEnum.Example);
        Raise(nameof(GuideSummary));
        Raise(nameof(GuideCountText));
        Raise(nameof(ExampleCountText));
    }

    /// <summary>
    /// Читает найденные задания занятия. <see langword="null"/>, если задания ещё не искали
    /// </summary>
    public Func<string, IReadOnlyList<(int Number, HomeworkItem Item, bool InSingularity)>?>? ItemsReader { get; set; }

    /// <summary>
    /// Проверяет готовую работу занятия и возвращает пустую строку или причину ошибки
    /// </summary>
    public Func<Lesson, Task<string>>? DraftChecker { get; set; }

    /// <summary>
    /// Находит отчёт о проверке по ключу занятия. Пусто, если проверки не было
    /// </summary>
    public Func<string, string>? CheckReader { get; set; }

    /// <summary>
    /// Убирает задание в архив: ключ занятия и номер задания
    /// </summary>
    public Action<string, int>? ItemArchived { get; set; }

    /// <summary>
    /// Находит адрес страницы задания в LMS по ключу занятия и номеру задания. Пусто, если задание не связано с работой LMS
    /// </summary>
    public Func<string, int, string>? ItemUrlReader { get; set; }

    /// <summary>
    /// Отмечает задание сданным или снимает отметку: ключ занятия, номер задания и новое значение
    /// </summary>
    public Action<string, int, bool>? ItemSubmitted { get; set; }

    /// <summary>
    /// Читает текст условия задания по ключу занятия и номеру задания
    /// </summary>
    public Func<string, int, string>? ItemConditionReader { get; set; }

    /// <summary>
    /// Заводит задачу в SingularityApp: ключ занятия и номер задания. Возвращает итог и признак ошибки
    /// </summary>
    public Func<string, int, Task<(string Message, bool Failed)>>? ItemToSingularity { get; set; }

    /// <summary>
    /// Удаляет готовые файлы занятия
    /// </summary>
    public Action<string>? DraftsDeleted { get; set; }

    /// <summary>
    /// Прикрепляет готовую работу владельца: ключ занятия, номер задания и путь к файлу. Возвращает итог и признак неудачи. Вызывается в фоновой задаче, потому что копирует файл
    /// </summary>
    public Func<string, int, string, (string Message, bool Failed)>? WorkAttacher { get; set; }

    /// <summary>
    /// Прикрепляет готовую работу владельца к заданию, не блокируя окно: файл копируется в фоновой задаче. Итог показывается в карточке занятия, списки файлов и заданий обновляются
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания в занятии</param>
    /// <param name="file">Путь к файлу владельца</param>
    /// <returns>Задача, которая завершается, когда файл скопирован и итог показан</returns>
    public async Task AttachWorkAsync(LessonFormatViewModel lesson, int number, string file)
    {
        var attach = WorkAttacher;
        if (attach is null)
        {
            lesson.ShowResult("Прикрепление работы не подключено", failed: true);

            return;
        }

        lesson.ShowResult("Копирую работу…", failed: false);
        (string Message, bool Failed) result;
        try
        {
            result = await Task.Run(() => attach(lesson.Id, number, file));
        }
        catch (Exception exception)
        {
            if (exception is not (IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException))
                System.Diagnostics.Trace.TraceError($"Прикрепление работы {lesson.Id}: {exception}");

            result = ("Не удалось прикрепить работу: " + exception.Message, true);
        }

        lesson.ShowResult(result.Message, result.Failed);
        RefreshDrafts(lesson);
        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Нужно открыть окно «Переделать» для занятия
    /// </summary>
    public event Action<LessonFormatViewModel>? RedoDialogRequested;

    /// <summary>
    /// Запоминает замечание из окна «Переделать»: занятие, текст, вид и признак «для всех предметов». Возвращает предупреждение или пустую строку. Вызывается в фоновой задаче: файл замечаний может быть занят до 10 секунд
    /// </summary>
    public Func<Lesson, string, RemarkKindEnum, bool, string>? RemarkSaver { get; set; }

    /// <summary>
    /// Читает все замечания владельца: общие и всех предметов. Вызывается в фоновой задаче
    /// </summary>
    public Func<IReadOnlyList<Remark>>? RemarkLister { get; set; }

    /// <summary>
    /// Заменяет замечание с тем же ключом, в том числе переносит его между файлом предмета и общим. Вызывается в фоновой задаче
    /// </summary>
    public Action<Remark>? RemarkUpdater { get; set; }

    /// <summary>
    /// Удаляет замечание по ключу. Вызывается в фоновой задаче
    /// </summary>
    public Action<string>? RemarkRemover { get; set; }

    /// <summary>
    /// Переделывает работы занятия по замечаниям из окна «Переделать»: замечание запоминается в фоне, подготовка начинается сразу и не ждёт записи. Если замечание записать не удалось, переделка всё равно идёт, а предупреждение дописывается к итогу в карточке
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="format">Формат прежней работы</param>
    /// <param name="text">Что не понравилось</param>
    /// <param name="files">Файлы с замечаниями преподавателя</param>
    /// <param name="kind">Вид замечания</param>
    /// <param name="global">Замечание для всех предметов</param>
    /// <returns>Задача, которая завершается, когда переделка закончена и итог показан</returns>
    public async Task RedoAsync(LessonFormatViewModel lesson, HomeworkFormatEnum format, string text, IReadOnlyList<string> files, RemarkKindEnum kind, bool global)
    {
        var save = RemarkSaver;
        var saving = save is null || text.Trim().Length == 0 ? Task.FromResult(string.Empty) : Task.Run(() => save(lesson.Lesson, text, kind, global));

        await lesson.PrepareAsync(new PrepareRequest(format, text, null, Redo: true, RemarkFiles: files));

        string warning;
        try
        {
            warning = await saving;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Замечание к переделке {lesson.Id}: {exception}");
            warning = "Замечание не запомнилось: " + exception.Message;
        }

        // Итог подготовки остаётся, предупреждение дописывается к нему: иначе подготовка затёрла бы его своим итогом
        if (warning.Length > 0)
            lesson.ShowResult(lesson.Status.Length > 0 ? lesson.Status + ". " + warning : warning, lesson.Failed);
    }

    /// <summary>
    /// Нужно спросить, удалять ли готовое задание занятия
    /// </summary>
    public event Action<LessonFormatViewModel>? DeleteConfirmRequested;

    /// <summary>
    /// Удаляет готовое задание занятия после подтверждения и обновляет карточку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    public void DeleteDrafts(LessonFormatViewModel lesson)
    {
        try
        {
            DraftsDeleted?.Invoke(lesson.Id);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            lesson.ShowResult("Не удалось удалить: закрой файл задания в Word или PowerPoint и повтори. " + exception.Message, failed: true);

            return;
        }

        RefreshDrafts(lesson);
        lesson.ShowResult("Готовое задание удалено", failed: false);
    }

    /// <summary>
    /// Читает шаг конвейера, файл работы, оценку и источник каждого задания занятия. Читает файлы, поэтому вызывается в фоновой задаче, а результат применяется в потоке окна. Пусто или ошибка чтения: карточки заданий нейтральные, без выделенного шага и значка
    /// </summary>
    public Func<Lesson, IReadOnlyDictionary<int, ItemPipelineInfo>>? PipelineReader { get; set; }

    /// <summary>
    /// Прочитанные задания занятия и их шаги, одним снимком
    /// </summary>
    /// <param name="Items">Задания или <see langword="null"/>, если их ещё не искали</param>
    /// <param name="Pipeline">Шаги заданий или <see langword="null"/>, если чтение шагов не подключено</param>
    private sealed record LessonData(IReadOnlyList<(int Number, HomeworkItem Item, bool InSingularity)>? Items, IReadOnlyDictionary<int, ItemPipelineInfo>? Pipeline);

    /// <summary>
    /// Прочитанные задания и шаги по ключу занятия: пока идёт новое чтение, карточка показывает прежние, поэтому строки не мигают
    /// </summary>
    private readonly Dictionary<string, LessonData> _data = new(StringComparer.Ordinal);

    /// <summary>
    /// Номер последнего запроса чтения шагов по ключу занятия: устаревший ответ не затирает свежий
    /// </summary>
    private readonly Dictionary<string, int> _pipelineVersions = new(StringComparer.Ordinal);

    /// <summary>
    /// Защищает кеш и применение прочитанного: в тестах без потока окна ответы приходят из пула потоков
    /// </summary>
    private readonly object _pipelineGate = new();

    /// <summary>
    /// Идущие чтения шагов
    /// </summary>
    private readonly List<Task> _pipelineLoads = [];

    /// <summary>
    /// Ждёт, пока прочитаются задания и шаги всех занятий, запрошенные к этому моменту
    /// </summary>
    /// <returns>Задача, которая завершается, когда все чтения применены</returns>
    public Task WhenPipelineLoaded()
    {
        lock (_pipelineGate)
            return Task.WhenAll(_pipelineLoads.ToArray());
    }

    /// <summary>
    /// Подтверждает задание: ключ занятия и номер задания. Вызывается в фоновой задаче
    /// </summary>
    public Action<string, int>? ItemConfirmer { get; set; }

    /// <summary>
    /// Записывает правку условия и замечание вида «условие»: ключ занятия, номер, название, требования. Возвращает предупреждение или пустую строку. Вызывается в фоновой задаче
    /// </summary>
    public Func<string, int, string, string, string>? ConditionFixer { get; set; }

    /// <summary>
    /// Делает работу только что сданного задания образцом предмета: ключ занятия и номер. Возвращает, что стало с образцом. Вызывается в фоновой задаче
    /// </summary>
    public Func<string, int, SampleOutcome>? SampleRegistrar { get; set; }

    /// <summary>
    /// Делает выбранный файл образцом сданного задания: ключ занятия, номер и путь. Возвращает причину неудачи или пустую строку. Вызывается в фоновой задаче
    /// </summary>
    public Func<string, int, string, string>? SubmittedFileSetter { get; set; }

    /// <summary>
    /// Нужно выбрать файл своей готовой работы и прикрепить его к заданию
    /// </summary>
    public event Action<LessonFormatViewModel, LessonItemViewModel>? AttachFileRequested;

    /// <summary>
    /// Нужно выбрать файл сданной работы задания
    /// </summary>
    public event Action<LessonFormatViewModel, LessonItemViewModel>? SubmittedFileRequested;

    /// <summary>
    /// Нужно открыть окно «Поправить условие» для задания
    /// </summary>
    public event Action<LessonFormatViewModel, LessonItemViewModel>? ConditionEditRequested;

    /// <summary>
    /// Задания, работу которых уже открывали в этом сеансе: ключи «занятие:номер». Тогда главная кнопка предлагает отметить сданным
    /// </summary>
    private readonly HashSet<string> _viewed = new(StringComparer.Ordinal);

    /// <summary>
    /// Ключ задания в наборе просмотренных работ
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <returns>Строка «занятие:номер»</returns>
    private static string ViewedKey(LessonFormatViewModel lesson, int number) => $"{lesson.Id}:{number}";

    /// <summary>
    /// Обновляет найденные задания занятия: сразу из прежнего снимка, а свежие задания и шаги читаются в фоне и применяются, когда готовы
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void RefreshItems(LessonFormatViewModel lesson) => _ = RefreshItemsAsync(lesson);

    /// <summary>
    /// Обновляет найденные задания занятия и ждёт свежего снимка. Файлы не читаются в потоке окна: задания и шаги читаются одной фоновой задачей
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Задача, которая завершается, когда снимок прочитан и применён; ошибок не выбрасывает</returns>
    private Task RefreshItemsAsync(LessonFormatViewModel lesson)
    {
        bool cached;
        Task load;
        lock (_pipelineGate)
        {
            cached = _data.ContainsKey(lesson.Id);
            var version = _pipelineVersions.GetValueOrDefault(lesson.Id) + 1;
            _pipelineVersions[lesson.Id] = version;
            load = LoadLessonAsync(lesson, ItemsReader, PipelineReader, version);
            Track(load);
        }

        // Строки собираются и объявляются вне блокировки: подписчики изменений не должны выполняться под ней
        if (cached)
            BuildItems(lesson);

        return load;
    }

    /// <summary>
    /// Запоминает идущее чтение, чтобы <see cref="WhenPipelineLoaded"/> его дождался; вызывается под блокировкой кеша
    /// </summary>
    /// <param name="task">Задача чтения или пересортировки</param>
    private void Track(Task task)
    {
        _pipelineLoads.RemoveAll(item => item.IsCompleted);
        _pipelineLoads.Add(task);
    }

    /// <summary>
    /// Занятия, у которых последнее чтение закончилось ошибкой и карточка показывает «Не удалось прочитать задания»: удачное чтение это сообщение убирает
    /// </summary>
    private readonly HashSet<string> _loadFailed = new(StringComparer.Ordinal);

    /// <summary>
    /// Читает задания и шаги занятия одной фоновой задачей и применяет их вместе в потоке окна, если ответ ещё свежий. Занятия здесь не пересортировываются: порядок меняет только <see cref="Update"/>, чтобы занятие не уезжало из-под курсора после действия владельца. Ошибка шагов оставляет прочитанные задания с нейтральной карточкой; ошибка заданий или сборки строк показывается в карточке, только если ответ ещё свежий
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="items">Чтение заданий</param>
    /// <param name="pipeline">Чтение шагов</param>
    /// <param name="version">Номер этого запроса</param>
    /// <returns>Задача чтения; ошибок не выбрасывает</returns>
    private async Task LoadLessonAsync(LessonFormatViewModel lesson, Func<string, IReadOnlyList<(int Number, HomeworkItem Item, bool InSingularity)>?>? items, Func<Lesson, IReadOnlyDictionary<int, ItemPipelineInfo>>? pipeline, int version)
    {
        try
        {
            var data = await Task.Run(() =>
            {
                var found = items?.Invoke(lesson.Id);
                if (found is null || pipeline is null)
                    return new LessonData(found, null);

                try
                {
                    return new LessonData(found, pipeline(lesson.Lesson));
                }
                catch (Exception exception)
                {
                    // Задания уже прочитаны: карточка покажет их без шагов, в нейтральном виде
                    System.Diagnostics.Trace.TraceError($"Шаги заданий {lesson.Id}: {exception}");

                    return new LessonData(found, null);
                }
            });

            bool clearError;
            lock (_pipelineGate)
            {
                if (_pipelineVersions.GetValueOrDefault(lesson.Id) != version)
                    return;

                _data[lesson.Id] = data;
                clearError = _loadFailed.Remove(lesson.Id);
            }

            if (clearError)
                lesson.ShowResult(string.Empty, failed: false);

            BuildItems(lesson);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Задания занятия {lesson.Id}: {exception}");
            lock (_pipelineGate)
            {
                // Устаревшее неудачное чтение не перекрывает более свежее удачное
                if (_pipelineVersions.GetValueOrDefault(lesson.Id) != version)
                    return;

                _loadFailed.Add(lesson.Id);
            }

            lesson.ShowResult("Не удалось прочитать задания занятия: " + exception.Message, failed: true);
        }
    }

    /// <summary>
    /// Пересортировывает занятия, когда все чтения одного <see cref="Update"/> применены: при запуске порядок меняется один раз, а не после каждого занятия
    /// </summary>
    /// <param name="loads">Чтения этого обновления; ошибок не выбрасывают</param>
    /// <returns>Задача пересортировки; ошибок не выбрасывает</returns>
    private async Task ReorderAfterAsync(IReadOnlyList<Task> loads)
    {
        try
        {
            await Task.WhenAll(loads);
            Reorder();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Порядок занятий: {exception}");
        }
    }

    /// <summary>
    /// Ставит занятия в порядок по свежим заданиям: с несданными сверху, без заданий ниже, всё сданное внизу, внутри группы от свежих к старым
    /// </summary>
    private void Reorder()
    {
        var rows = Lessons.OrderBy(row => PipelineStepper.LessonGroup(row.Items.Count, row.PendingCount)).ThenByDescending(row => row.Lesson.Date).ToList();
        if (rows.SequenceEqual(Lessons))
            return;

        Lessons = rows;
        UpdateSubjects();
    }

    /// <summary>
    /// Собирает строки заданий из последнего прочитанного снимка, не трогая файлы
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void BuildItems(LessonFormatViewModel lesson)
    {
        LessonData? data;
        lock (_pipelineGate)
            data = _data.GetValueOrDefault(lesson.Id);

        var found = data?.Items;
        var pipeline = data?.Pipeline;
        var actions = CardActions(lesson);
        lesson.SetItems(
            [.. (found ?? []).Select(row => new LessonItemViewModel(
                row.Number,
                row.Item,
                row.InSingularity,
                entry => ArchiveItem(lesson, row.Number),
                entry => Forget(AddToSingularityAsync(lesson, row.Number), lesson),
                entry => OpenItemInLms(lesson, row.Number),
                entry => Forget(MarkSubmittedAsync(lesson, row.Number, !entry.Submitted), lesson),
                entry => CopyCondition(lesson, row.Number),
                pipeline is not null && pipeline.TryGetValue(row.Number, out var info) ? info : null,
                actions,
                _viewed.Contains(ViewedKey(lesson, row.Number))))],
            found is not null);
    }

    /// <summary>
    /// Запускает действие карточки без ожидания, но так, что ни одно исключение не теряется: оно пишется в журнал и показывается в карточке
    /// </summary>
    /// <param name="task">Действие</param>
    /// <param name="lesson">Занятие, в карточке которого показать ошибку</param>
    private static async void Forget(Task task, LessonFormatViewModel lesson)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Действие с заданием {lesson.Id}: {exception}");
            lesson.ShowResult("Действие не удалось: " + exception.Message, failed: true);
        }
    }

    /// <summary>
    /// Действия карточки задания для занятия: главная кнопка, вопрос, источник и меню «…». Действия с готовой работой относятся ко всему занятию, как и раньше
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Набор действий</returns>
    private ItemCardActions CardActions(LessonFormatViewModel lesson) => new()
    {
        ViewWork = item =>
        {
            _viewed.Add(ViewedKey(lesson, item.Number));
            PathOpened?.Invoke(item.WorkPath);
            BuildItems(lesson);
        },
        Confirm = item => Forget(ConfirmAsync(lesson, item.Number), lesson),
        FixCondition = item => ConditionEditRequested?.Invoke(lesson, item),
        Recheck = _ => Run(lesson.CheckCommand),
        OpenCheck = _ => Run(lesson.OpenCheckCommand),
        Redo = _ => Run(lesson.RedoCommand),
        AttachWork = item => AttachFileRequested?.Invoke(lesson, item),
        SubmittedFile = item => SubmittedFileRequested?.Invoke(lesson, item),
        DeleteDrafts = _ => Run(lesson.DeleteCommand),
        OpenTranscript = lesson.HasTranscript ? _ => ShowTranscriptOf(lesson) : null,
    };

    /// <summary>
    /// Выполняет команду занятия, если она сейчас доступна: например, проверка не запускается второй раз, пока идёт первая
    /// </summary>
    /// <param name="command">Команда</param>
    private static void Run(RelayCommand command)
    {
        if (command.CanExecute(null))
            command.Execute(null);
    }

    /// <summary>
    /// Переключает на вкладку «Расшифровки» и открывает читалку на занятии
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void ShowTranscriptOf(LessonFormatViewModel lesson)
    {
        HomeworkTab = false;
        OpenTranscript(lesson);
    }

    /// <summary>
    /// Показывает ошибку фоновой записи в карточке занятия; неожиданную ошибку (не файлов и не данных) ещё и пишет в журнал
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="prefix">Начало сообщения владельцу</param>
    /// <param name="exception">Исключение</param>
    private static void Fail(LessonFormatViewModel lesson, string prefix, Exception exception)
    {
        if (exception is not (IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or System.Text.Json.JsonException))
            System.Diagnostics.Trace.TraceError($"Задание {lesson.Id}: {exception}");

        lesson.ShowResult(prefix + exception.Message, failed: true);
    }

    /// <summary>
    /// Подтверждает задание в фоновой задаче, показывает итог и обновляет карточку: вопрос пропадает, работу подготовит ближайшая выгрузка. Агент здесь не запускается
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <returns>Задача, которая завершается, когда подтверждение записано и итог показан</returns>
    public async Task ConfirmAsync(LessonFormatViewModel lesson, int number)
    {
        var confirm = ItemConfirmer;
        if (confirm is null)
        {
            lesson.ShowResult("Подтверждение не подключено", failed: true);

            return;
        }

        try
        {
            await Task.Run(() => confirm(lesson.Id, number));
            lesson.ShowResult("Подтверждено: работа подготовится при ближайшей выгрузке", failed: false);
        }
        catch (Exception exception)
        {
            Fail(lesson, "Не удалось сохранить подтверждение: ", exception);
        }

        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Записывает правку условия в фоновой задаче: задание подтверждается, правка сохраняется замечанием вида «условие» для этого предмета
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <param name="title">Верное название</param>
    /// <param name="requirements">Верные требования</param>
    /// <returns>Задача, которая завершается, когда правка записана и итог показан</returns>
    public async Task FixConditionAsync(LessonFormatViewModel lesson, int number, string title, string requirements)
    {
        var fix = ConditionFixer;
        if (fix is null)
        {
            lesson.ShowResult("Правка условия не подключена", failed: true);

            return;
        }

        try
        {
            var warning = await Task.Run(() => fix(lesson.Id, number, title, requirements));
            // Предупреждение значит, что правка сохранена и не записалось только замечание: это не ошибка
            lesson.ShowResult(warning.Length > 0 ? warning : "Условие поправлено: работа подготовится при ближайшей выгрузке", failed: false);
        }
        catch (Exception exception)
        {
            Fail(lesson, "Не удалось сохранить правку условия: ", exception);
        }

        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Отмечает задание сданным или снимает отметку в фоновой задаче. При сдаче файл работы становится образцом предмета; ошибка образца показывается, но отметку не отменяет
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <param name="submitted">Новое значение</param>
    /// <returns>Задача, которая завершается, когда отметка записана и итог показан</returns>
    public async Task MarkSubmittedAsync(LessonFormatViewModel lesson, int number, bool submitted)
    {
        var mark = ItemSubmitted;
        var register = SampleRegistrar;
        try
        {
            var outcome = await Task.Run(() =>
            {
                mark?.Invoke(lesson.Id, number, submitted);

                return submitted && register is not null ? register(lesson.Id, number) : null;
            });
            if (outcome is { Warning.Length: > 0 })
                lesson.ShowResult(outcome.Warning, failed: true);
            else
                lesson.ShowResult(!submitted ? "Отметка «сдано» снята" : outcome switch
                {
                    { Registered: true } => "Отмечено сданным, работа стала образцом предмета",
                    { KeptOwners: true } => "Отмечено сданным, образцом остаётся указанный тобой файл",
                    _ => "Отмечено сданным",
                }, failed: false);
        }
        catch (Exception exception)
        {
            Fail(lesson, "Не удалось сохранить отметку: ", exception);
        }

        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Делает выбранный файл образцом сданного задания в фоновой задаче и показывает итог
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <param name="file">Путь к сданному файлу</param>
    /// <returns>Задача, которая завершается, когда образец записан и итог показан</returns>
    public async Task SetSubmittedFileAsync(LessonFormatViewModel lesson, int number, string file)
    {
        var set = SubmittedFileSetter;
        if (set is null)
        {
            lesson.ShowResult("Образцы не подключены", failed: true);

            return;
        }

        lesson.ShowResult("Записываю образец…", failed: false);
        try
        {
            var error = await Task.Run(() => set(lesson.Id, number, file));
            lesson.ShowResult(error.Length > 0 ? error : "Файл сданной работы стал образцом предмета", error.Length > 0);
        }
        catch (Exception exception)
        {
            Fail(lesson, "Образец не записался: ", exception);
        }

        RefreshGuides();
        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Открывает страницу задания в LMS или говорит, почему не вышло
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    private void OpenItemInLms(LessonFormatViewModel lesson, int number)
    {
        var url = ItemUrlReader?.Invoke(lesson.Id, number) ?? string.Empty;
        if (url.Length == 0)
            lesson.ShowResult("Задание не связано с работой в LMS: страницу открыть нельзя", failed: true);
        else
            PathOpened?.Invoke(url);
    }

    /// <summary>
    /// Копирует условие задания в буфер обмена
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    private void CopyCondition(LessonFormatViewModel lesson, int number)
    {
        var text = ItemConditionReader?.Invoke(lesson.Id, number) ?? string.Empty;
        if (text.Length == 0)
        {
            lesson.ShowResult("Условия нет: задание не найдено", failed: true);

            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            lesson.ShowResult("Условие скопировано в буфер обмена", failed: false);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            lesson.ShowResult("Буфер обмена занят другой программой, повтори", failed: true);
        }
    }

    /// <summary>
    /// Убирает задание в архив и обновляет список
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    private void ArchiveItem(LessonFormatViewModel lesson, int number)
    {
        ItemArchived?.Invoke(lesson.Id, number);
        RefreshItems(lesson);
    }

    /// <summary>
    /// Заводит задачу в SingularityApp, показывает итог и обновляет список
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="number">Номер задания</param>
    /// <returns>Задача, которая завершается, когда SingularityApp ответил</returns>
    private async Task AddToSingularityAsync(LessonFormatViewModel lesson, int number)
    {
        lesson.ShowResult("Завожу задачу в Singularity…", failed: false);
        var result = ItemToSingularity is null ? ("Singularity не подключён", true) : await ItemToSingularity(lesson.Id, number);
        lesson.ShowResult(result.Item1, result.Item2);
        RefreshItems(lesson);
    }

    /// <summary>
    /// Находит готовые файлы заданий по ключу занятия
    /// </summary>
    public Func<string, IReadOnlyList<string>>? DraftLister { get; set; }

    /// <summary>
    /// Открыта вкладка «Домашние задания»
    /// </summary>
    public bool HomeworkTab
    {
        get => _homeworkTab;
        private set
        {
            if (!Set(ref _homeworkTab, value))
                return;

            Raise(nameof(IsTranscriptsTab));
            Raise(nameof(IsHomeworkTab));
        }
    }

    /// <summary>
    /// Открыта вкладка «Расшифровки»
    /// </summary>
    public bool IsTranscriptsTab => !_homeworkTab;

    /// <summary>
    /// Открыта вкладка «Домашние задания»
    /// </summary>
    public bool IsHomeworkTab => _homeworkTab;

    /// <summary>
    /// Открывает окно «Методические указания»
    /// </summary>
    public RelayCommand ManageGuidesCommand { get; }

    /// <summary>
    /// Нужно открыть окно «Методические указания» выбранного предмета
    /// </summary>
    public event Action? GuidesDialogRequested;

    /// <summary>
    /// Нужно открыть окно «Подготовить задание» для занятия
    /// </summary>
    public event Action<LessonFormatViewModel>? PrepareDialogRequested;

    /// <summary>
    /// Переключает на вкладку «Расшифровки»
    /// </summary>
    public RelayCommand ShowTranscriptsCommand { get; }

    /// <summary>
    /// Переключает на вкладку «Домашние задания»
    /// </summary>
    public RelayCommand ShowHomeworkCommand { get; }

    /// <summary>
    /// Закрывает открытую расшифровку
    /// </summary>
    public RelayCommand CloseLessonCommand { get; }

    /// <summary>
    /// Копирует конспект к практике по открытой расшифровке
    /// </summary>
    public RelayCommand CopyBriefCommand { get; }

    /// <summary>
    /// Кладёт в буфер обмена конспект к практике: что повторить и что знать по открытому занятию
    /// </summary>
    private void CopyBrief()
    {
        if (OpenLesson is not { } lesson)
            return;

        var text = Steward.Preview.PracticeBrief.Build(lesson.Title, lesson.Date, _reading);
        if (text.Length == 0)
        {
            lesson.ShowResult("Конспекта пока нет: подробный разбор появится после ближайшей выгрузки", failed: true);

            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(text);
            lesson.ShowResult("Конспект к практике скопирован в буфер обмена", failed: false);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            lesson.ShowResult("Буфер обмена занят другой программой, повтори", failed: true);
        }
    }

    /// <summary>
    /// Занятие, расшифровка которого открыта
    /// </summary>
    public LessonFormatViewModel? OpenLesson
    {
        get => _openLesson;
        private set
        {
            if (Set(ref _openLesson, value))
                Raise(nameof(HasOpenLesson));
        }
    }

    /// <summary>
    /// Расшифровка открыта
    /// </summary>
    public bool HasOpenLesson => OpenLesson is not null;

    /// <summary>
    /// Тема открытой встречи
    /// </summary>
    public string ReadingTopic => _reading.Topic;

    /// <summary>
    /// Что было на занятии, связным текстом
    /// </summary>
    public string ReadingOverview => _reading.Overview;

    /// <summary>
    /// Ключевые моменты: что пройдено на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingPoints => _reading.Points;

    /// <summary>
    /// Принятые решения
    /// </summary>
    public IReadOnlyList<string> ReadingDecisions => _reading.Decisions;

    /// <summary>
    /// Задачи, которые прозвучали на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingTasks => _reading.Tasks;

    /// <summary>
    /// Разобранные темы с подробностями
    /// </summary>
    public IReadOnlyList<AnalysisTopic> ReadingTopics => _reading.Topics;

    /// <summary>
    /// Важные формулировки, которые прозвучали на занятии
    /// </summary>
    public IReadOnlyList<string> ReadingFormulations => _reading.Formulations;

    /// <summary>
    /// Есть разобранные темы
    /// </summary>
    public bool HasReadingTopics => _reading.Topics.Count > 0;

    /// <summary>
    /// Есть формулировки
    /// </summary>
    public bool HasReadingFormulations => _reading.Formulations.Count > 0;

    /// <summary>
    /// Подробного разбора ещё нет: показано только то, что было в резюме MTS Link
    /// </summary>
    public bool ReadingBrief => !_reading.Detailed && !ReadingEmpty;

    /// <summary>
    /// Есть принятые решения
    /// </summary>
    public bool HasReadingDecisions => _reading.Decisions.Count > 0;

    /// <summary>
    /// Есть задачи
    /// </summary>
    public bool HasReadingTasks => _reading.Tasks.Count > 0;

    /// <summary>
    /// Кто сколько говорил, в процентах от всех слов
    /// </summary>
    public IReadOnlyList<ChartShareViewModel> ReadingSpeakers => [.. _reading.Speakers.Take(6).Select(speaker => new ChartShareViewModel(speaker.Name, _reading.Words == 0 ? 0 : (int)Math.Round(100.0 * speaker.Words / _reading.Words), _reading.Speakers[0].Words == 0 ? 0 : (double)speaker.Words / _reading.Speakers[0].Words, PanelToneEnum.Idle))];

    /// <summary>
    /// Есть статистика по репликам
    /// </summary>
    public bool HasReadingSpeakers => _reading.Speakers.Count > 0;

    /// <summary>
    /// Показатели занятия: дата, длительность, слов, участников
    /// </summary>
    public IReadOnlyList<StatusTileViewModel> ReadingStats => OpenLesson is null ? [] :
    [
        new StatusTileViewModel("Дата", OpenLesson.Date, PanelToneEnum.Idle),
        new StatusTileViewModel("Длительность", OpenLesson.HasDuration ? OpenLesson.Duration : "не указана", PanelToneEnum.Idle),
        new StatusTileViewModel("Пройдено", _reading.Points.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelToneEnum.Idle),
        new StatusTileViewModel("Участников", _reading.Speakers.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), PanelToneEnum.Idle),
        new StatusTileViewModel("Слов", _reading.Words.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("ru-RU")), PanelToneEnum.Idle),
    ];

    /// <summary>
    /// Сколько пунктов пройдено на занятии
    /// </summary>
    public string ReadingCount => _reading.Points.Count == 0 ? "Пунктов нет" : $"Пройдено пунктов: {_reading.Points.Count}";

    /// <summary>
    /// У встречи есть тема
    /// </summary>
    public bool HasReadingTopic => _reading.Topic.Length > 0;

    /// <summary>
    /// Есть ключевые моменты
    /// </summary>
    public bool HasReadingPoints => _reading.Points.Count > 0;

    /// <summary>
    /// Есть краткое содержание
    /// </summary>
    public bool HasReadingOverview => _reading.Overview.Length > 0;

    /// <summary>
    /// Разбор расшифровки пустой: резюме в файле нет
    /// </summary>
    public bool ReadingEmpty => _reading.Topic.Length == 0 && _reading.Overview.Length == 0 && _reading.Points.Count == 0;

    /// <summary>
    /// Запоминает работы из LMS и обновляет счётчики на плитках предметов
    /// </summary>
    /// <param name="activities">Задания и тесты</param>
    public void ApplyActivities(IReadOnlyList<ActivityInfo> activities)
    {
        _activities = activities;
        foreach (var subject in Subjects)
            subject.SetActivities([.. activities.Where(item => string.Equals(item.Subject, subject.Title, StringComparison.OrdinalIgnoreCase))]);
    }

    /// <summary>
    /// Запоминает задания, найденные в расшифровках, и раскладывает их по предметам
    /// </summary>
    /// <param name="rows">Задания с занятиями</param>
    public void ApplyHomework(IReadOnlyList<HomeworkRow> rows)
    {
        _homeworkCards = [.. rows.Select(row => new HomeworkCardViewModel(row))];
        foreach (var subject in Subjects)
            subject.Homework = [.. _homeworkCards.Where(card => card.Subject == subject.Title)];
    }

    /// <summary>
    /// Показывает расшифровку занятия: резюме и полный текст
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void OpenTranscript(LessonFormatViewModel lesson)
    {
        foreach (var other in Lessons)
            other.IsOpen = other == lesson;

        _reading = TranscriptText.Parse(TranscriptReader?.Invoke(lesson.Id) ?? string.Empty, AnalysisReader?.Invoke(lesson.Id));
        OpenLesson = lesson;
        RaiseReading();
    }

    /// <summary>
    /// Закрывает расшифровку
    /// </summary>
    private void CloseLesson()
    {
        foreach (var lesson in Lessons)
            lesson.IsOpen = false;

        OpenLesson = null;
        _reading = TranscriptText.Empty;
        RaiseReading();
    }

    /// <summary>
    /// Сообщает окну, что разбор расшифровки изменился
    /// </summary>
    private void RaiseReading()
    {
        Raise(nameof(ReadingTopic));
        Raise(nameof(ReadingOverview));
        Raise(nameof(ReadingPoints));
        Raise(nameof(ReadingCount));
        Raise(nameof(ReadingEmpty));
        Raise(nameof(HasReadingTopic));
        Raise(nameof(HasReadingPoints));
        Raise(nameof(HasReadingOverview));
        Raise(nameof(ReadingDecisions));
        Raise(nameof(ReadingTasks));
        Raise(nameof(HasReadingDecisions));
        Raise(nameof(ReadingTopics));
        Raise(nameof(ReadingFormulations));
        Raise(nameof(HasReadingTopics));
        Raise(nameof(HasReadingFormulations));
        Raise(nameof(ReadingBrief));
        Raise(nameof(HasReadingTasks));
        Raise(nameof(ReadingSpeakers));
        Raise(nameof(HasReadingSpeakers));
        Raise(nameof(ReadingStats));
    }

    /// <summary>
    /// Обновляет список готовых файлов занятия, не пересоздавая его, пока файлы те же
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void RefreshDrafts(LessonFormatViewModel lesson)
    {
        lesson.HasCheck = (CheckReader?.Invoke(lesson.Id) ?? string.Empty).Length > 0;
        var paths = DraftLister?.Invoke(lesson.Id) ?? [];
        if (paths.SequenceEqual(lesson.Drafts.Select(draft => draft.Path)))
            return;

        lesson.Drafts = [.. paths.Select(path => new DraftFileViewModel(path, file => PathOpened?.Invoke(file), paths.Count == 1))];
    }

    /// <summary>
    /// Обновляет список. Строки известных занятий остаются теми же, поэтому идущая подготовка и выбор на экране не сбрасываются
    /// </summary>
    /// <param name="lessons">Занятия с выбранными форматами от свежих к старым</param>
    public void Update(IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons)
    {
        _formatByTitle = lessons.GroupBy(item => item.Lesson.Title, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Format, StringComparer.Ordinal);
        var known = Lessons.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var loads = new List<Task>();
        var rows = lessons
            .Select(item => (item.Lesson.Date, Row: BuildRow(item)))
            .OrderBy(entry => PipelineStepper.LessonGroup(entry.Row.Items.Count, entry.Row.PendingCount))
            .ThenByDescending(entry => entry.Date)
            .Select(entry => entry.Row)
            .ToList();

        if (!rows.SequenceEqual(Lessons))
            Lessons = rows;

        UpdateSubjects();

        // Свежий порядок применяется один раз, когда прочитаны все занятия этого обновления
        lock (_pipelineGate)
            Track(ReorderAfterAsync(loads));

        LessonFormatViewModel BuildRow((Lesson Lesson, HomeworkFormatEnum Format) item)
        {
            if (!known.TryGetValue(item.Lesson.Id, out var row))
            {
                row = new LessonFormatViewModel(item.Lesson, item.Format, _chosen, _hasTranscript(item.Lesson), _prepare, _fetchRequested) { Opened = OpenTranscript, PrepareRequested = lesson => PrepareDialogRequested?.Invoke(lesson), CheckRequested = lesson => Forget(CheckAndRefreshAsync(lesson), lesson), CheckOpened = lesson => PathOpened?.Invoke(CheckReader?.Invoke(lesson.Id) ?? string.Empty), RedoRequested = lesson => RedoDialogRequested?.Invoke(lesson), DeleteRequested = lesson => DeleteConfirmRequested?.Invoke(lesson) };
                RefreshDrafts(row);
                loads.Add(RefreshItemsAsync(row));

                return row;
            }

            row.HasTranscript = _hasTranscript(item.Lesson);
            RefreshDrafts(row);
            loads.Add(RefreshItemsAsync(row));

            return row;
        }
    }

    /// <summary>
    /// Проверяет готовую работу занятия и обновляет карточку: задание с работой переходит на шаг «Проверено»
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Задача, которая завершается, когда проверка закончена и карточка обновлена</returns>
    private async Task CheckAndRefreshAsync(LessonFormatViewModel lesson)
    {
        await lesson.RunCheckAsync(DraftChecker ?? (_ => Task.FromResult("Проверка не подключена")));
        RefreshDrafts(lesson);
        await RefreshItemsAsync(lesson);
    }

    /// <summary>
    /// Собирает карточки предметов из строк занятий: занятия с одним названием идут в одну карточку, порядок предметов по названию
    /// </summary>
    private void UpdateSubjects()
    {
        var known = Subjects.ToDictionary(subject => subject.Title, StringComparer.Ordinal);
        var lessonsByTitle = _formatByTitle;
        var subjects = Lessons
            .GroupBy(row => row.Title, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .Select(group =>
            {
                if (known.TryGetValue(group.Key, out var subject))
                {
                    subject.Update([.. group]);

                    return subject;
                }

                var rows = group.ToArray();
                var format = lessonsByTitle.TryGetValue(group.Key, out var found) ? found : HomeworkFormats.DEFAULT;

                return new SubjectViewModel(group.Key, rows, _stateOf?.Invoke(group.Key) ?? LessonStateEnum.Undecided, _stateChosen, format, _subjectFormatChosen, Select);
            })
            .ToList();

        if (!subjects.SequenceEqual(Subjects))
            Subjects = subjects;

        ApplyActivities(_activities);
        foreach (var subject in subjects)
            subject.Homework = [.. _homeworkCards.Where(card => card.Subject == subject.Title)];

        // Выбранный предмет остаётся выбранным после обновления, а если выбора ещё не было, открывается первый
        Select(subjects.FirstOrDefault(subject => subject.Title == SelectedSubject?.Title) ?? subjects.FirstOrDefault());
    }

    /// <summary>
    /// Делает предмет выбранным: его плитка подсвечивается, а панель с занятиями открывается под плитками
    /// </summary>
    /// <param name="subject">Предмет или <see langword="null"/>, если предметов нет</param>
    private void Select(SubjectViewModel? subject)
    {
        foreach (var other in Subjects)
            other.IsSelected = other == subject;

        SelectedSubject = subject;
        RefreshGuides();
    }
}