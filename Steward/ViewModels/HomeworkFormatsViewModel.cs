using System.Windows.Documents;
using Steward.Homework.Models;
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
    private readonly Func<Lesson, Task<(int Count, string Error)>> _prepare;

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
    private TranscriptText _reading = new(string.Empty, string.Empty, []);

    /// <summary>
    /// Готовый файл, открытый в предпросмотре
    /// </summary>
    private DraftFileViewModel? _openDraft;

    /// <summary>
    /// Документ предпросмотра
    /// </summary>
    private FlowDocument? _previewDocument;

    /// <summary>
    /// Работы из LMS
    /// </summary>
    private IReadOnlyList<ActivityInfo> _activities = [];

    /// <summary>
    /// Задания, найденные в расшифровках
    /// </summary>
    private IReadOnlyList<HomeworkCardViewModel> _homeworkCards = [];

    /// <summary>
    /// Создаёт модель без занятий
    /// </summary>
    /// <param name="chosen">Вызывается при выборе формата: параметры — ключ занятия и формат</param>
    /// <param name="hasTranscript">Проверяет, есть ли у занятия расшифровка</param>
    /// <param name="prepare">Ищет задания в расшифровке занятия: возвращает их число и текст ошибки</param>
    /// <param name="stateChosen">Запоминает отметку предмета: ключ — название предмета. Пусто: выбор отметок не показывается</param>
    /// <param name="stateOf">Читает отметку предмета по названию</param>
    public HomeworkFormatsViewModel(
        Action<string, HomeworkFormatEnum> chosen,
        Func<Lesson, bool> hasTranscript,
        Func<Lesson, Task<(int Count, string Error)>> prepare,
        Action<string, LessonStateEnum>? stateChosen = null,
        Func<string, LessonStateEnum>? stateOf = null,
        Action<Lesson>? fetchRequested = null,
        Action<string, HomeworkFormatEnum>? subjectFormatChosen = null)
    {
        ShowTranscriptsCommand = new RelayCommand(() => HomeworkTab = false);
        ShowHomeworkCommand = new RelayCommand(() => HomeworkTab = true);
        CloseLessonCommand = new RelayCommand(CloseLesson);
        CloseDraftCommand = new RelayCommand(CloseDraft);
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
    public HomeworkFormatsViewModel(
        IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons,
        Action<string, HomeworkFormatEnum> chosen,
        Func<Lesson, bool> hasTranscript,
        Func<Lesson, Task<(int Count, string Error)>> prepare,
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
    /// Занятий пока нет
    /// </summary>
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

    public bool IsEmpty => Lessons.Count == 0;

    /// <summary>
    /// Читает текст расшифровки по ключу занятия
    /// </summary>
    public Func<string, string>? TranscriptReader { get; set; }

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
    /// Закрывает предпросмотр файла
    /// </summary>
    public RelayCommand CloseDraftCommand { get; }

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
    /// Готовый файл, открытый в предпросмотре
    /// </summary>
    public DraftFileViewModel? OpenDraft
    {
        get => _openDraft;
        private set
        {
            if (Set(ref _openDraft, value))
                Raise(nameof(HasPreview));
        }
    }

    /// <summary>
    /// Предпросмотр файла открыт
    /// </summary>
    public bool HasPreview => OpenDraft is not null;

    /// <summary>
    /// Документ предпросмотра
    /// </summary>
    public FlowDocument? PreviewDocument
    {
        get => _previewDocument;
        private set => Set(ref _previewDocument, value);
    }

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

        _reading = TranscriptText.Parse(TranscriptReader?.Invoke(lesson.Id) ?? string.Empty);
        RaiseReading();
        OpenLesson = lesson;
    }

    /// <summary>
    /// Закрывает расшифровку
    /// </summary>
    private void CloseLesson()
    {
        foreach (var lesson in Lessons)
            lesson.IsOpen = false;

        OpenLesson = null;
        _reading = new TranscriptText(string.Empty, string.Empty, []);
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
    }

    /// <summary>
    /// Открывает предпросмотр готового файла
    /// </summary>
    /// <param name="file">Файл</param>
    private void OpenDraftFile(DraftFileViewModel file)
    {
        foreach (var lesson in Lessons)
        {
            foreach (var other in lesson.Drafts)
                other.IsOpen = other == file;
        }

        PreviewDocument = DocumentPreviewBuilder.Build(file.Path);
        OpenDraft = file;
    }

    /// <summary>
    /// Закрывает предпросмотр
    /// </summary>
    private void CloseDraft()
    {
        foreach (var lesson in Lessons)
        {
            foreach (var other in lesson.Drafts)
                other.IsOpen = false;
        }

        OpenDraft = null;
        PreviewDocument = null;
    }

    /// <summary>
    /// Обновляет список готовых файлов занятия, не пересоздавая его, пока файлы те же
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void RefreshDrafts(LessonFormatViewModel lesson)
    {
        var paths = DraftLister?.Invoke(lesson.Id) ?? [];
        if (paths.SequenceEqual(lesson.Drafts.Select(draft => draft.Path)))
            return;

        lesson.Drafts = [.. paths.Select(path => new DraftFileViewModel(path, OpenDraftFile))];
    }

    /// <summary>
    /// Обновляет список. Строки известных занятий остаются теми же, поэтому идущая подготовка и выбор на экране не сбрасываются
    /// </summary>
    /// <param name="lessons">Занятия с выбранными форматами от свежих к старым</param>
    public void Update(IReadOnlyList<(Lesson Lesson, HomeworkFormatEnum Format)> lessons)
    {
        _formatByTitle = lessons.GroupBy(item => item.Lesson.Title, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Format, StringComparer.Ordinal);
        var known = Lessons.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var rows = lessons
            .Select(item =>
            {
                if (!known.TryGetValue(item.Lesson.Id, out var row))
                {
                    row = new LessonFormatViewModel(item.Lesson, item.Format, _chosen, _hasTranscript(item.Lesson), _prepare, _fetchRequested) { Opened = OpenTranscript };
                    RefreshDrafts(row);

                    return row;
                }

                row.HasTranscript = _hasTranscript(item.Lesson);
                RefreshDrafts(row);

                return row;
            })
            .ToList();

        if (!rows.SequenceEqual(Lessons))
            Lessons = rows;

        UpdateSubjects();
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
    }
}