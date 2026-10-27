using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Карточка предмета в настройках: отметка «что делать с расшифровками», сколько занятий и сколько расшифровок уже есть, и список занятий внутри
/// </summary>
public sealed class SubjectViewModel : Bindable
{
    /// <summary>
    /// Список занятий раскрыт
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Занятия предмета от свежих к старым
    /// </summary>
    private IReadOnlyList<LessonFormatViewModel> _lessons;

    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="title">Название предмета</param>
    /// <param name="lessons">Занятия предмета</param>
    /// <param name="state">Текущая отметка предмета</param>
    /// <param name="stateChosen">Вызывается при выборе отметки: ключ (название предмета) и отметка. Пусто: выбор не показывается</param>
    /// <param name="format">Формат заданий по умолчанию для предмета</param>
    /// <param name="formatChosen">Вызывается при выборе формата по умолчанию: название предмета и формат. Пусто: выбор формата не показывается</param>
    public SubjectViewModel(string title, IReadOnlyList<LessonFormatViewModel> lessons, LessonStateEnum state, Action<string, LessonStateEnum>? stateChosen, HomeworkFormatEnum format = HomeworkFormats.DEFAULT, Action<string, HomeworkFormatEnum>? formatChosen = null)
    {
        Title = title;
        _lessons = lessons;
        var options = new List<LessonStateOptionViewModel>();
        if (stateChosen is not null)
        {
            foreach (var option in Enum.GetValues<LessonStateEnum>())
            {
                options.Add(new LessonStateOptionViewModel(option, option == state, picked =>
                {
                    foreach (var other in options)
                        other.IsSelected = other.State == picked;

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
    /// Занятия предмета от свежих к старым
    /// </summary>
    public IReadOnlyList<LessonFormatViewModel> Lessons
    {
        get => _lessons;
        private set
        {
            if (!Set(ref _lessons, value))
                return;

            Raise(nameof(Summary));
        }
    }

    /// <summary>
    /// Список занятий раскрыт
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>
    /// Сколько занятий и у скольких уже есть расшифровка, одной строкой
    /// </summary>
    public string Summary => $"Занятий: {Lessons.Count}  ·  расшифровано: {Lessons.Count(lesson => lesson.HasTranscript)}  ·  последнее: {Lessons.FirstOrDefault()?.Date}";

    /// <summary>
    /// Обновляет занятия предмета, когда список изменился или у какого-то появилась расшифровка
    /// </summary>
    /// <param name="lessons">Занятия предмета</param>
    public void Update(IReadOnlyList<LessonFormatViewModel> lessons)
    {
        if (lessons.SequenceEqual(Lessons))
        {
            Raise(nameof(Summary));

            return;
        }

        Lessons = lessons;
    }
}
