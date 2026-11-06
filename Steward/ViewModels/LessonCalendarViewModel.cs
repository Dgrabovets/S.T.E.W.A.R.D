using System.Globalization;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Месячный календарь занятий предмета: дни с точками по состоянию занятий, переключение месяцев, нажатие на день открывает расшифровку
/// </summary>
public sealed class LessonCalendarViewModel : Bindable
{
    /// <summary>
    /// Откуда брать занятия предмета
    /// </summary>
    private readonly Func<IReadOnlyList<LessonFormatViewModel>> _lessons;

    /// <summary>
    /// Первый день показанного месяца
    /// </summary>
    private DateOnly _month = FirstOfMonth(DateOnly.FromDateTime(DateTime.Today));

    /// <summary>
    /// Пользователь сам листал месяцы: тогда календарь не прыгает к свежему занятию
    /// </summary>
    private bool _navigated;

    /// <summary>
    /// Дни месяца вместе с хвостами соседних недель
    /// </summary>
    private IReadOnlyList<CalendarDayViewModel> _days = [];

    /// <summary>
    /// Создаёт календарь
    /// </summary>
    /// <param name="lessons">Источник занятий предмета</param>
    public LessonCalendarViewModel(Func<IReadOnlyList<LessonFormatViewModel>> lessons)
    {
        _lessons = lessons;
        PreviousCommand = new RelayCommand(() => Move(-1));
        NextCommand = new RelayCommand(() => Move(1));
        Rebuild();
    }

    /// <summary>
    /// Дни месяца вместе с хвостами соседних недель, неделя с понедельника
    /// </summary>
    public IReadOnlyList<CalendarDayViewModel> Days
    {
        get => _days;
        private set => Set(ref _days, value);
    }

    /// <summary>
    /// Заголовок: месяц и год, например «Ноябрь 2026»
    /// </summary>
    public string Title => CultureInfo.GetCultureInfo("ru-RU").TextInfo.ToTitleCase(_month.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("ru-RU")));

    /// <summary>
    /// Предыдущий месяц
    /// </summary>
    public RelayCommand PreviousCommand { get; }

    /// <summary>
    /// Следующий месяц
    /// </summary>
    public RelayCommand NextCommand { get; }

    /// <summary>
    /// Пересобирает дни по текущим занятиям. Пока месяцы не листали, показывается месяц открытого занятия, а без него месяц самого свежего
    /// </summary>
    public void Rebuild()
    {
        var lessons = _lessons();
        if (!_navigated)
        {
            var anchor = lessons.FirstOrDefault(lesson => lesson.IsOpen) ?? lessons.OrderByDescending(lesson => lesson.Lesson.Date).FirstOrDefault();
            _month = FirstOfMonth(anchor?.Lesson.Date ?? DateOnly.FromDateTime(DateTime.Today));
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var last = _month.AddMonths(1).AddDays(-1);
        var start = _month.AddDays(-(((int)_month.DayOfWeek + 6) % 7));
        var end = last.AddDays(6 - ((int)last.DayOfWeek + 6) % 7);
        var days = new List<CalendarDayViewModel>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var current = day;
            days.Add(new CalendarDayViewModel(day, day.Month == _month.Month, day == today, [.. lessons.Where(lesson => lesson.Lesson.Date == current)]));
        }

        Days = days;
        Raise(nameof(Title));
    }

    /// <summary>
    /// Листает месяцы
    /// </summary>
    /// <param name="months">На сколько месяцев сдвинуться, минус назад</param>
    private void Move(int months)
    {
        _navigated = true;
        _month = _month.AddMonths(months);
        Rebuild();
    }

    /// <summary>
    /// Первый день месяца, в который попадает дата
    /// </summary>
    /// <param name="date">Дата</param>
    /// <returns>Первое число её месяца</returns>
    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
