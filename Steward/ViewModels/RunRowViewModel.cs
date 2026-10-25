using System.Globalization;
using Steward.Models;
using Steward.Status.Registry;
using Steward.Status.Running;

namespace Steward.ViewModels;

/// <summary>
/// Строка журнала запусков
/// </summary>
public sealed class RunRowViewModel
{
    /// <summary>
    /// Создаёт строку по записи журнала
    /// </summary>
    /// <param name="record">Запись о запуске</param>
    public RunRowViewModel(RunRecord record)
    {
        When = record.StartedAt.ToLocalTime().ToString("dd.MM HH:mm:ss", CultureInfo.InvariantCulture);
        Title = record.Title;
        Reason = record.Reason switch
        {
            JobTrigger.START => "при запуске приложения",
            JobTrigger.DAILY => "по расписанию",
            JobTrigger.INTERVAL => "по интервалу",
            "manual" => "вручную",
            _ => record.Reason,
        };

        Outcome = record.Outcome switch
        {
            RunOutcomeEnum.Ok => "Успешно",
            RunOutcomeEnum.Failed => "Ошибка",
            RunOutcomeEnum.TimedOut => "Не уложилась во время",
            _ => "Не запустилась",
        };

        Detail = record.Detail ?? string.Empty;
        Tone = record.Outcome == RunOutcomeEnum.Ok ? PanelToneEnum.Running : PanelToneEnum.Problem;
    }

    /// <summary>
    /// Когда запущена
    /// </summary>
    public string When { get; }

    /// <summary>
    /// Название задачи
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Почему запущена
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Чем закончилась
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// Пояснение к ошибке
    /// </summary>
    public string Detail { get; }

    /// <summary>
    /// Есть что показать под названием: у успешных запусков пояснения нет
    /// </summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>
    /// Цвет точки
    /// </summary>
    public PanelToneEnum Tone { get; }
}
