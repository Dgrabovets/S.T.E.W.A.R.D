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
        _state = state;
        _format = format;
        SelectCommand = new RelayCommand(() => selected?.Invoke(this));
        var options = new List<LessonStateOptionViewModel>();
        if (stateChosen is not null)
        {
            foreach (var option in Enum.GetValues<LessonStateEnum>())
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
        }
    }

    /// <summary>
    /// Подпись внизу плитки: отметка и формат, например «Расшифровывать · Word»
    /// </summary>
    public string Caption => State == LessonStateEnum.Fetch ? $"{LessonStateOptionViewModel.TitleOf(State)} · {HomeworkFormats.Title(Format)}" : LessonStateOptionViewModel.TitleOf(State);

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

            RaiseCounters();
        }
    }

    /// <summary>
    /// У скольких занятий уже есть расшифровка
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
    /// Сообщает окну, что числа на плитке изменились
    /// </summary>
    private void RaiseCounters()
    {
        Raise(nameof(Transcribed));
        Raise(nameof(Total));
        Raise(nameof(Progress));
        Raise(nameof(Note));
        Raise(nameof(Summary));
    }
}
