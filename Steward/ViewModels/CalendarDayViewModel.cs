using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Один день в календаре занятий предмета: число, есть ли занятие, расшифровано ли оно и есть ли в нём неразобранные задания
/// </summary>
public sealed class CalendarDayViewModel
{
    /// <summary>
    /// Создаёт день
    /// </summary>
    /// <param name="date">Дата дня</param>
    /// <param name="inMonth">День относится к показанному месяцу, а не к соседнему</param>
    /// <param name="isToday">Сегодня</param>
    /// <param name="lessons">Занятия этого дня; пусто, если занятий нет</param>
    /// <param name="goTo">Показывает месяц этой даты: вызывается по нажатию на день соседнего месяца. Пусто: день только открывает расшифровку</param>
    public CalendarDayViewModel(DateOnly date, bool inMonth, bool isToday, IReadOnlyList<LessonFormatViewModel> lessons, Action<DateOnly>? goTo = null)
    {
        Number = date.Day;
        InMonth = inMonth;
        IsToday = isToday;
        HasLesson = lessons.Count > 0;
        HasTranscript = lessons.Any(lesson => lesson.HasTranscript);
        HasOpenAssignments = lessons.Any(lesson => lesson.HasOpenAssignments);
        IsOpen = lessons.Any(lesson => lesson.IsOpen);

        // День соседнего месяца сначала переключает календарь на его месяц; день с расшифровкой затем открывает её, а без расшифровки больше ничего не делает
        var target = lessons.FirstOrDefault(lesson => lesson.HasTranscript);
        var switches = !inMonth && goTo is not null;
        ClickCommand = new RelayCommand(() =>
        {
            if (switches)
                goTo!(date);

            target?.OpenCommand.Execute(null);
        }, () => target is not null || switches);
    }

    /// <summary>
    /// Число месяца
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// День относится к показанному месяцу
    /// </summary>
    public bool InMonth { get; }

    /// <summary>
    /// Сегодня
    /// </summary>
    public bool IsToday { get; }

    /// <summary>
    /// В этот день было занятие
    /// </summary>
    public bool HasLesson { get; }

    /// <summary>
    /// Хотя бы одно занятие дня расшифровано
    /// </summary>
    public bool HasTranscript { get; }

    /// <summary>
    /// В занятиях дня есть задания, на которые ещё не отреагировали
    /// </summary>
    public bool HasOpenAssignments { get; }

    /// <summary>
    /// Расшифровка одного из занятий дня сейчас открыта
    /// </summary>
    public bool IsOpen { get; }

    /// <summary>
    /// Открывает расшифровку занятия дня
    /// </summary>
    public RelayCommand ClickCommand { get; }
}
