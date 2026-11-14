using System.Globalization;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Месячный календарь занятий предмета: дни с точками по состоянию занятий, переключение месяцев, нажатие на день открывает расшифровку
/// </summary>
public sealed class LessonCalendarViewModel : Bindable
{
    /// <summary>
    /// Сколько недель в сетке всегда
    /// </summary>
    private const int WEEKS = 6;

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
        OpenPickerCommand = new RelayCommand(OpenPicker);
        PreviousYearCommand = new RelayCommand(() => ShiftYear(-1));
        NextYearCommand = new RelayCommand(() => ShiftYear(1));
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
    /// Переворот листа сетки дней при смене месяца
    /// </summary>
    public FlipState MonthFlip { get; } = new();

    /// <summary>
    /// Переворот листа месяцев в окошке выбора при смене года
    /// </summary>
    public FlipState YearFlip { get; } = new();

    /// <summary>
    /// Окошко выбора месяца открыто
    /// </summary>
    public bool IsPickerOpen
    {
        get => _isPickerOpen;
        set => Set(ref _isPickerOpen, value);
    }

    /// <summary>
    /// Окошко выбора месяца открыто
    /// </summary>
    private bool _isPickerOpen;

    /// <summary>
    /// Год, месяцы которого показаны в окошке выбора
    /// </summary>
    private int _pickerYear = DateTime.Today.Year;

    /// <summary>
    /// Год в заголовке окошка выбора месяца
    /// </summary>
    public string PickerYearText => _pickerYear.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Двенадцать месяцев выбранного года для окошка выбора
    /// </summary>
    public IReadOnlyList<MonthChoiceViewModel> MonthChoices => [.. Enumerable.Range(1, 12).Select(number =>
    {
        var first = new DateOnly(_pickerYear, number, 1);
        var name = CultureInfo.GetCultureInfo("ru-RU").TextInfo.ToTitleCase(first.ToDateTime(TimeOnly.MinValue).ToString("MMMM", CultureInfo.GetCultureInfo("ru-RU")));

        return new MonthChoiceViewModel(name, first == _month, _lessons().Any(lesson => lesson.Lesson.Date.Year == _pickerYear && lesson.Lesson.Date.Month == number), new RelayCommand(() => Choose(first)));
    })];

    /// <summary>
    /// Открывает окошко выбора месяца на году показанного месяца
    /// </summary>
    public RelayCommand OpenPickerCommand { get; }

    /// <summary>
    /// Предыдущий год в окошке выбора
    /// </summary>
    public RelayCommand PreviousYearCommand { get; }

    /// <summary>
    /// Следующий год в окошке выбора
    /// </summary>
    public RelayCommand NextYearCommand { get; }

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
        // Всегда шесть недель: высота календаря не прыгает при переходе между месяцами в 4, 5 и 6 строк, а лишние дни соседних месяцев переключают месяц
        var end = start.AddDays(WEEKS * 7 - 1);
        var days = new List<CalendarDayViewModel>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            var current = day;
            days.Add(new CalendarDayViewModel(day, day.Month == _month.Month, day == today, [.. lessons.Where(lesson => lesson.Lesson.Date == current)], GoTo));
        }

        Days = days;
        Raise(nameof(Title));
        Raise(nameof(MonthChoices));
    }

    /// <summary>
    /// Открывает окошко выбора месяца
    /// </summary>
    private void OpenPicker()
    {
        _pickerYear = _month.Year;
        Raise(nameof(PickerYearText));
        Raise(nameof(MonthChoices));
        IsPickerOpen = true;
    }

    /// <summary>
    /// Листает годы в окошке выбора
    /// </summary>
    /// <param name="years">На сколько лет сдвинуться, минус назад</param>
    private void ShiftYear(int years) => YearFlip.Run(Math.Sign(years), () =>
    {
        _pickerYear += years;
        Raise(nameof(PickerYearText));
        Raise(nameof(MonthChoices));
    });

    /// <summary>
    /// Показывает месяц даты: по нажатию на день соседнего месяца в сетке
    /// </summary>
    /// <param name="date">Дата дня, на который нажали</param>
    private void GoTo(DateOnly date) => MonthFlip.Run(FirstOfMonth(date).CompareTo(_month), () =>
    {
        _navigated = true;
        _month = FirstOfMonth(date);
        Rebuild();
    });

    /// <summary>
    /// Показывает выбранный в окошке месяц и закрывает окошко
    /// </summary>
    /// <param name="first">Первый день выбранного месяца</param>
    private void Choose(DateOnly first)
    {
        IsPickerOpen = false;
        MonthFlip.Run(first.CompareTo(_month), () =>
        {
            _navigated = true;
            _month = first;
            Rebuild();
        });
    }

    /// <summary>
    /// Листает месяцы
    /// </summary>
    /// <param name="months">На сколько месяцев сдвинуться, минус назад</param>
    private void Move(int months) => MonthFlip.Run(Math.Sign(months), () =>
    {
        _navigated = true;
        _month = _month.AddMonths(months);
        Rebuild();
    });

    /// <summary>
    /// Первый день месяца, в который попадает дата
    /// </summary>
    /// <param name="date">Дата</param>
    /// <returns>Первое число её месяца</returns>
    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
