using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Предмет в настройках: плитка с числом расшифрованных занятий и прогрессом, а под плитками, когда она выбрана, панель с отметкой предмета, форматом по умолчанию и занятиями
/// </summary>
public sealed class SubjectViewModel : Bindable
{
    /// <summary>
    /// Плитка выбрана: под плитками открыта панель этого предмета
    /// </summary>
    private bool _isSelected;

    /// <summary>
    /// Занятия предмета от свежих к старым
    /// </summary>
    private IReadOnlyList<LessonFormatViewModel> _lessons;

    /// <summary>
    /// Текущая отметка предмета
    /// </summary>
    private LessonStateEnum _state;

    /// <summary>
    /// Формат заданий по умолчанию
    /// </summary>
    private HomeworkFormatEnum _format;

    /// <summary>
    /// Создаёт плитку
    /// </summary>
    /// <param name="title">Название предмета</param>
    /// <param name="lessons">Занятия предмета</param>
    /// <param name="state">Текущая отметка предмета</param>
    /// <param name="stateChosen">Вызывается при выборе отметки: ключ (название предмета) и отметка. Пусто: выбор не показывается</param>
    /// <param name="format">Формат заданий по умолчанию для предмета</param>
    /// <param name="formatChosen">Вызывается при выборе формата по умолчанию: название предмета и формат. Пусто: выбор формата не показывается</param>
    /// <param name="selected">Вызывается по нажатию на плитку: просит сделать этот предмет выбранным</param>
    public SubjectViewModel(string title, IReadOnlyList<LessonFormatViewModel> lessons, LessonStateEnum state, Action<string, LessonStateEnum>? stateChosen, HomeworkFormatEnum format = HomeworkFormats.DEFAULT, Action<string, HomeworkFormatEnum>? formatChosen = null, Action<SubjectViewModel>? selected = null)
    {
        Title = title;
        _lessons = lessons;
        Watch(lessons);
        _state = state;
        _format = format;
        SelectCommand = new RelayCommand(() => selected?.Invoke(this));
        RefineCommand = new RelayCommand(() => RefineRequested?.Invoke(this));
        var options = new List<LessonStateOptionViewModel>();
        if (stateChosen is not null)
        {
            foreach (var option in new[] { LessonStateEnum.Undecided, LessonStateEnum.Fetch, LessonStateEnum.Analyze, LessonStateEnum.LmsOnly, LessonStateEnum.Skip, LessonStateEnum.Done })
            {
                options.Add(new LessonStateOptionViewModel(option, option == state, picked =>
                {
                    foreach (var other in options)
                        other.IsSelected = other.State == picked;

                    State = picked;
                    stateChosen(title, picked);
                }));
            }
        }

        StateOptions = options;

        var formats = new List<FormatOptionViewModel>();
        if (formatChosen is not null)
        {
            foreach (var option in HomeworkFormats.All.Where(item => item != HomeworkFormatEnum.Skip))
            {
                formats.Add(new FormatOptionViewModel(option, option == format, picked =>
                {
                    foreach (var other in formats)
                        other.IsSelected = other.Format == picked;

                    foreach (var lesson in Lessons)
                        lesson.SetDefaultFormat(picked);

                    Format = picked;
                    formatChosen(title, picked);
                }));
            }
        }

        FormatOptions = formats;
    }

    /// <summary>
    /// Название предмета
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Кнопка «Уточнить»: открывает окно, в котором выбирается, какие общие указания и образцы относятся к предмету
    /// </summary>
    public RelayCommand RefineCommand { get; }

    /// <summary>
    /// Вызывается по кнопке «Уточнить»: просит окно открыть выбор указаний предмета
    /// </summary>
    public Action<SubjectViewModel>? RefineRequested { get; set; }

    /// <summary>
    /// Настройки предмета для домашних заданий: автоподготовка и откуда берутся задания. Пусто, пока окно не подключило хранилище
    /// </summary>
    public SubjectOptionsViewModel? Options { get; set; }

    /// <summary>
    /// Нажатие на плитку: делает предмет выбранным
    /// </summary>
    public RelayCommand SelectCommand { get; }

    /// <summary>
    /// Плитка выбрана
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
    }

    /// <summary>
    /// Кнопки выбора отметки предмета
    /// </summary>
    public IReadOnlyList<LessonStateOptionViewModel> StateOptions { get; }

    /// <summary>
    /// Есть кнопки отметок
    /// </summary>
    public bool HasStates => StateOptions.Count > 0;

    /// <summary>
    /// Кнопки выбора формата заданий по умолчанию для предмета. Формат «Не готовить» здесь не предлагается
    /// </summary>
    public IReadOnlyList<FormatOptionViewModel> FormatOptions { get; }

    /// <summary>
    /// Есть кнопки формата
    /// </summary>
    public bool HasFormats => FormatOptions.Count > 0;

    /// <summary>
    /// Выбранный пункт списка «Что делать»: список в плитке выбирает его, а модель передаёт выбор дальше
    /// </summary>
    public LessonStateOptionViewModel? SelectedStateOption
    {
        get => StateOptions.FirstOrDefault(option => option.IsSelected);
        set
        {
            if (value is not null && !value.IsSelected)
                value.SelectCommand.Execute(null);
        }
    }

    /// <summary>
    /// Выбранный пункт списка форматов заданий
    /// </summary>
    public FormatOptionViewModel? SelectedFormatOption
    {
        get => FormatOptions.FirstOrDefault(option => option.IsSelected);
        set
        {
            if (value is not null && !value.IsSelected)
                value.SelectCommand.Execute(null);
        }
    }

    /// <summary>
    /// Список форматов нужен: по предмету готовятся домашние задания (расшифровки забираются или задания берутся из LMS)
    /// </summary>
    public bool ShowFormat => HasFormats && State is LessonStateEnum.Fetch or LessonStateEnum.LmsOnly;

    /// <summary>
    /// Текущая отметка предмета: от неё зависит цвет кружка на плитке
    /// </summary>
    public LessonStateEnum State
    {
        get => _state;
        private set
        {
            if (!Set(ref _state, value))
                return;

            Raise(nameof(Caption));
            Raise(nameof(ShowFormat));
            Raise(nameof(SelectedStateOption));
            Raise(nameof(IsCompleted));
        }
    }

    /// <summary>
    /// Формат заданий по умолчанию
    /// </summary>
    public HomeworkFormatEnum Format
    {
        get => _format;
        private set
        {
            if (!Set(ref _format, value))
                return;

            Raise(nameof(Caption));
            Raise(nameof(SelectedFormatOption));
        }
    }

    /// <summary>
    /// Подпись внизу плитки: отметка и формат, например «Расшифровывать · Word» или «Задания из LMS · Word»
    /// </summary>
    public string Caption => State is LessonStateEnum.Fetch or LessonStateEnum.LmsOnly ? $"{LessonStateOptionViewModel.TitleOf(State)} · {HomeworkFormats.Title(Format)}" : LessonStateOptionViewModel.TitleOf(State);

    /// <summary>
    /// Занятия предмета от свежих к старым
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> Lessons
    {
        get => _lessons;
        private set
        {
            if (!Set(ref _lessons, value))
                return;

            Watch(value);
            RaiseCounters();
            RaiseOpenAssignments();
        }
    }

    /// <summary>
    /// Подписывается на изменения занятий и отмечает свежее. Вызывается и для занятий, с которыми создана плитка
    /// </summary>
    /// <param name="value">Занятия предмета</param>
    private void Watch(IReadOnlyList<LessonFormatViewModel> value)
    {
        // Задания занятий читаются в фоне и появляются позже самой строки: список вкладки обновляется, когда у занятия они появились
        foreach (var lesson in value.Where(_watched.Add))
            lesson.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(LessonFormatViewModel.HasItems) or nameof(LessonFormatViewModel.HasTranscript))
                    Raise(nameof(HomeworkLessons));
                else if (args.PropertyName == nameof(LessonFormatViewModel.OpenAssignments))
                    RaiseOpenAssignments();
            };

        // Свежее занятие одно, последнее по дате: остальные подписываются возрастом и приглушаются
        var newest = value.Select(lesson => lesson.Lesson.Date).DefaultIfEmpty().Max();
        foreach (var lesson in value)
            lesson.IsCurrent = lesson.Lesson.Date == newest;
    }

    /// <summary>
    /// Сколько занятий расшифровано, словами для плитки
    /// </summary>
    public string TranscribedText => $"Расшифровано {Transcribed} из {Total}";

    /// <summary>
    /// Занятия, у которых уже слушается изменение заданий и расшифровки
    /// </summary>
    private readonly HashSet<LessonFormatViewModel> _watched = [];

    /// <summary>
    /// Работы предмета из LMS: задания и тесты
    /// </summary>
    private IReadOnlyList<Steward.Homework.Models.ActivityInfo> _activities = [];

    /// <summary>
    /// Задания предмета, найденные в расшифровках
    /// </summary>
    private IReadOnlyList<HomeworkCardViewModel> _homework = [];

    /// <summary>
    /// Запоминает работы предмета из LMS и обновляет счётчики плитки
    /// </summary>
    /// <param name="activities">Работы этого предмета</param>
    public void SetActivities(IReadOnlyList<Steward.Homework.Models.ActivityInfo> activities)
    {
        _activities = activities;
        Raise(nameof(AssignmentsText));
        Raise(nameof(AssignmentsProgress));
        Raise(nameof(QuizzesText));
        Raise(nameof(QuizzesProgress));
        Raise(nameof(HasQuizzes));
    }

    /// <summary>
    /// «Сдано 2 из 3»
    /// </summary>
    public string AssignmentsText => _activities.Count(item => !item.IsQuiz) is var total and > 0 ? $"Сдано {_activities.Count(item => !item.IsQuiz && item.Done)} из {total}" : "Заданий нет";

    /// <summary>
    /// Доля сданных заданий
    /// </summary>
    public double AssignmentsProgress => _activities.Count(item => !item.IsQuiz) is var total and > 0 ? (double)_activities.Count(item => !item.IsQuiz && item.Done) / total : 0;

    /// <summary>
    /// «Тесты 1 из 2»
    /// </summary>
    public string QuizzesText => _activities.Count(item => item.IsQuiz) is var total and > 0 ? $"Тесты {_activities.Count(item => item.IsQuiz && item.Done)} из {total}" : "Тестов нет";

    /// <summary>
    /// Доля пройденных тестов
    /// </summary>
    public double QuizzesProgress => _activities.Count(item => item.IsQuiz) is var total and > 0 ? (double)_activities.Count(item => item.IsQuiz && item.Done) / total : 0;

    /// <summary>
    /// Предмет отмечен «Завершён»: на плитке лента в углу, а счётчики остаются живыми, потому что работы в LMS могут ещё меняться
    /// </summary>
    public bool IsCompleted => State == LessonStateEnum.Done;

    /// <summary>
    /// У предмета есть тесты
    /// </summary>
    public bool HasQuizzes => _activities.Any(item => item.IsQuiz);

    /// <summary>
    /// Задания предмета, найденные в расшифровках
    /// </summary>
    public IReadOnlyList<HomeworkCardViewModel> Homework
    {
        get => _homework;
        set
        {
            if (Set(ref _homework, value))
                Raise(nameof(HasHomework));
        }
    }

    /// <summary>
    /// По предмету найдены задания
    /// </summary>
    public bool HasHomework => Homework.Count > 0;

    /// <summary>
    /// Занятия для вкладки «Домашние задания»: с расшифровкой или с заданиями. Задания из LMS кладутся и к занятию без расшифровки, а когда расшифровка появится, она подтянется в ту же плашку
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> HomeworkLessons => [.. Lessons.Where(lesson => lesson.HasTranscript || lesson.HasItems)];

    /// <summary>
    /// У скольких занятий уже есть расшифровка
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> LessonsWithTranscript => [.. Lessons.Where(lesson => lesson.HasTranscript)];

    /// <summary>
    /// Сколько занятий расшифровано
    /// </summary>
    public int Transcribed => Lessons.Count(lesson => lesson.HasTranscript);

    /// <summary>
    /// Сколько всего занятий
    /// </summary>
    public int Total => Lessons.Count;

    /// <summary>
    /// Доля расшифрованных занятий от нуля до единицы для полосы прогресса
    /// </summary>
    public double Progress => Total == 0 ? 0 : (double)Transcribed / Total;

    /// <summary>
    /// Пояснение к крупному числу на плитке
    /// </summary>
    public string Note => $"из {Total} расшифровано";

    /// <summary>
    /// Строка под названием в панели предмета: сколько занятий и когда было последнее
    /// </summary>
    public string Summary => $"Занятий: {Total}  ·  расшифровано: {Transcribed}  ·  последнее: {Lessons.FirstOrDefault()?.Date}";

    /// <summary>
    /// Обновляет занятия предмета, когда список изменился или у какого-то появилась расшифровка
    /// </summary>
    /// <param name="lessons">Занятия предмета</param>
    public void Update(IReadOnlyList<LessonFormatViewModel> lessons)
    {
        if (lessons.SequenceEqual(Lessons))
        {
            RaiseCounters();

            return;
        }

        Lessons = lessons;
    }

    /// <summary>
    /// Сколько заданий из разбора во всех занятиях предмета ещё без реакции владельца
    /// </summary>
    public int OpenAssignments => Lessons.Sum(lesson => lesson.OpenAssignments);

    /// <summary>
    /// В занятиях предмета есть задания без реакции владельца: на плитке горит плашка
    /// </summary>
    public bool HasOpenAssignments => OpenAssignments > 0;

    /// <summary>
    /// Плашка на плитке: «Не разобрано: 3»
    /// </summary>
    public string OpenAssignmentsText => $"Не разобрано: {OpenAssignments}";

    /// <summary>
    /// Сообщает окну, что число неразобранных заданий изменилось
    /// </summary>
    private void RaiseOpenAssignments()
    {
        Raise(nameof(OpenAssignments));
        Raise(nameof(HasOpenAssignments));
        Raise(nameof(OpenAssignmentsText));
    }

    /// <summary>
    /// Сообщает окну, что числа на плитке изменились
    /// </summary>
    private void RaiseCounters()
    {
        Raise(nameof(LessonsWithTranscript));
        Raise(nameof(HomeworkLessons));
        Raise(nameof(TranscribedText));
        Raise(nameof(Transcribed));
        Raise(nameof(Total));
        Raise(nameof(Progress));
        Raise(nameof(Note));
        Raise(nameof(Summary));
    }
}
