using Steward.Homework.Models;
using Steward.Mvvm;

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
    /// Формат заданий по умолчанию для каждого предмета, по названию
    /// </summary>
    private IReadOnlyDictionary<string, HomeworkFormatEnum> _formatByTitle = new Dictionary<string, HomeworkFormatEnum>();

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

    public bool IsEmpty => Lessons.Count == 0;

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
                    return new LessonFormatViewModel(item.Lesson, item.Format, _chosen, _hasTranscript(item.Lesson), _prepare, _fetchRequested);

                row.HasTranscript = _hasTranscript(item.Lesson);

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

                return new SubjectViewModel(group.Key, rows, _stateOf?.Invoke(group.Key) ?? LessonStateEnum.Undecided, _stateChosen, format, _subjectFormatChosen);
            })
            .ToList();

        if (!subjects.SequenceEqual(Subjects))
            Subjects = subjects;
    }
}