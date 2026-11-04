using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Вкладка в левой панели: название, сфера в цвете состояния процесса, число того, что ждёт решения, и отметка, что вкладка открыта
/// </summary>
public sealed class NavItemViewModel : Bindable
{
    /// <summary>
    /// Какая вкладка открыта сейчас
    /// </summary>
    private readonly Func<AppPageEnum> _current;

    /// <summary>
    /// Открывает вкладку
    /// </summary>
    private readonly Action<AppPageEnum> _select;

    /// <summary>
    /// Вид сферы
    /// </summary>
    private ActivityKindEnum _kind = ActivityKindEnum.Dormant;

    /// <summary>
    /// Что показывает индикатор процесса
    /// </summary>
    private ActivityStateEnum _activity;

    /// <summary>
    /// Когда процесс закончился
    /// </summary>
    private DateTimeOffset _activitySince;

    /// <summary>
    /// Цвет сферы
    /// </summary>
    private PanelToneEnum _tone = PanelToneEnum.Idle;

    /// <summary>
    /// Сколько всего ждёт решения
    /// </summary>
    private int _badge;

    /// <summary>
    /// Создаёт вкладку
    /// </summary>
    /// <param name="page">Страница, которую вкладка открывает</param>
    /// <param name="title">Название вкладки</param>
    /// <param name="rowId">Идентификатор задачи, чьё состояние показывает сфера. Пусто: сфера нейтральная или задаётся отдельно</param>
    /// <param name="current">Какая страница открыта сейчас</param>
    /// <param name="select">Открывает страницу</param>
    public NavItemViewModel(AppPageEnum page, string title, string? rowId, Func<AppPageEnum> current, Action<AppPageEnum> select)
    {
        Page = page;
        Title = title;
        RowId = rowId;
        _current = current;
        _select = select;
    }

    /// <summary>
    /// Страница, которую вкладка открывает
    /// </summary>
    public AppPageEnum Page { get; }

    /// <summary>
    /// Название вкладки
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Идентификатор задачи, чьё состояние показывает сфера
    /// </summary>
    public string? RowId { get; }

    /// <summary>
    /// Вкладка открыта. Установка открывает её, снятие игнорируется: его делает выбор соседней вкладки
    /// </summary>
    public bool IsSelected
    {
        get => _current() == Page;
        set
        {
            if (value)
                _select(Page);
        }
    }

    /// <summary>
    /// Вид сферы
    /// </summary>
    public ActivityKindEnum Kind
    {
        get => _kind;
        private set => Set(ref _kind, value);
    }

    /// <summary>
    /// Что показывает индикатор процесса
    /// </summary>
    public ActivityStateEnum Activity
    {
        get => _activity;
        private set => Set(ref _activity, value);
    }

    /// <summary>
    /// Когда процесс закончился
    /// </summary>
    public DateTimeOffset ActivitySince
    {
        get => _activitySince;
        private set => Set(ref _activitySince, value);
    }

    /// <summary>
    /// Цвет сферы
    /// </summary>
    public PanelToneEnum Tone
    {
        get => _tone;
        private set => Set(ref _tone, value);
    }

    /// <summary>
    /// Сколько всего ждёт решения: показывается числом справа
    /// </summary>
    public int Badge
    {
        get => _badge;
        private set
        {
            if (!Set(ref _badge, value))
                return;

            Raise(nameof(HasBadge));
        }
    }

    /// <summary>
    /// Есть что показать числом
    /// </summary>
    public bool HasBadge => Badge > 0;

    /// <summary>
    /// Сообщает, что открытая страница изменилась
    /// </summary>
    public void RefreshSelection() => Raise(nameof(IsSelected));

    /// <summary>
    /// Задаёт вид, цвет и число сферы по состоянию процесса
    /// </summary>
    /// <param name="kind">Вид сферы</param>
    /// <param name="activity">Что показывает индикатор</param>
    /// <param name="since">Когда процесс закончился</param>
    /// <param name="tone">Цвет сферы</param>
    /// <param name="badge">Число справа</param>
    public void Update(ActivityKindEnum kind, ActivityStateEnum activity, DateTimeOffset since, PanelToneEnum tone, int badge)
    {
        Kind = kind;
        Activity = activity;
        ActivitySince = since;
        Tone = tone;
        Badge = badge;
    }
}
