using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Строка напоминания на экране «Статус»: что сдать, по какому предмету и через сколько дней срок
/// </summary>
public sealed class DeadlineCardViewModel
{
    /// <summary>
    /// За сколько дней до срока строка получает цвет внимания
    /// </summary>
    private const int ATTENTION_DAYS = 2;

    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="deadline">Срок сдачи</param>
    /// <param name="today">Сегодняшняя дата</param>
    public DeadlineCardViewModel(Deadline deadline, DateOnly today)
    {
        Title = deadline.Title;
        Subject = deadline.Subject;
        When = DeadlineBuilder.When(deadline.Due, today);
        Date = deadline.Due.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
        Tone = deadline.Due < today ? PanelToneEnum.Problem : deadline.Due.DayNumber - today.DayNumber <= ATTENTION_DAYS ? PanelToneEnum.Attention : PanelToneEnum.Idle;
    }

    /// <summary>
    /// Что нужно сдать
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Предмет
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Через сколько дней срок: «сегодня», «завтра», «через N дн.» или «просрочено на N дн.»
    /// </summary>
    public string When { get; }

    /// <summary>
    /// Дата срока
    /// </summary>
    public string Date { get; }

    /// <summary>
    /// Цвет подписи срока: красный у просроченного, жёлтый у срока в ближайшие дни
    /// </summary>
    public PanelToneEnum Tone { get; }
}
