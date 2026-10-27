using System.Globalization;
using Steward.Models;
using Steward.Mvvm;
using Steward.Monitoring.Models;

namespace Steward.ViewModels;

/// <summary>
/// Строка журнала: одна сверка расписания с таблицей
/// </summary>
public sealed class ScheduleCheckViewModel
{
    /// <summary>
    /// Создаёт строку по записи истории сверок
    /// </summary>
    /// <param name="entry">Запись сверки</param>
    /// <param name="remove">Вызывается, когда человек удаляет запись из журнала</param>
    public ScheduleCheckViewModel(LogEntry entry, Action<LogEntry>? remove = null)
    {
        RemoveCommand = new RelayCommand(() => remove?.Invoke(entry));
        When = entry.Time.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        Outcome = entry.Tone switch
        {
            LogToneEnum.Update => "Появились правки",
            LogToneEnum.Problem => "Не удалось обновить",
            _ => "Всё совпало",
        };

        Tone = entry.Tone switch
        {
            LogToneEnum.Update => PanelToneEnum.Attention,
            LogToneEnum.Problem => PanelToneEnum.Problem,
            _ => PanelToneEnum.Running,
        };

        Lines = Split(entry);
        Reason = "по интервалу";
        Facts = entry.Duration is { } duration ? JournalWording.Duration(duration) : string.Empty;
        var stamp = entry.Time.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);
        var lineTone = Tone == PanelToneEnum.Running ? PanelToneEnum.Idle : Tone;
        OutputLines = [.. Lines.Select(line => new LogLine(stamp, line, lineTone))];
        IsProblem = entry.Tone == LogToneEnum.Problem;
        Repeats = entry.Tone == LogToneEnum.Quiet && entry.Repeat > 1 ? JournalWording.Repeats(entry.Repeat) : string.Empty;
    }

    /// <summary>
    /// Удаляет эту запись из журнала
    /// </summary>
    public RelayCommand RemoveCommand { get; }

    /// <summary>
    /// Когда закончилась сверка: день, часы и минуты
    /// </summary>
    public string When { get; }

    /// <summary>
    /// Чем запущена сверка: сервис расписания всегда проверяет таблицу по интервалу
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Сколько шла сверка. Пусто у записей без этих данных
    /// </summary>
    public string Facts { get; }

    /// <summary>
    /// Есть подпись о длительности сверки
    /// </summary>
    public bool HasFacts => Facts.Length > 0;

    /// <summary>
    /// Строки записи в тех же рамках, что и вывод запуска задачи
    /// </summary>
    public IReadOnlyList<LogLine> OutputLines { get; }

    /// <summary>
    /// Чем закончилась
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// Цвет плашки с итогом
    /// </summary>
    public PanelToneEnum Tone { get; }

    /// <summary>
    /// Что изменилось или в чём причина сбоя, по строке на пункт
    /// </summary>
    public IReadOnlyList<string> Lines { get; }

    /// <summary>
    /// Сверка не удалась: строки показываются как причина
    /// </summary>
    public bool IsProblem { get; }

    /// <summary>
    /// Для правок: строки перечисляют изменения и выделяются отдельным блоком
    /// </summary>
    public bool HasChanges => Tone == PanelToneEnum.Attention && Lines.Count > 0;

    /// <summary>
    /// Строки нужно показать выделенным блоком: правки или причина сбоя
    /// </summary>
    public bool HasBlock => Tone != PanelToneEnum.Running && Lines.Count > 0;

    /// <summary>
    /// Для тихой сверки: у неё есть строка подробностей
    /// </summary>
    public bool HasQuietDetail => Tone == PanelToneEnum.Running && Lines.Count > 0;

    /// <summary>
    /// Сколько таких же тихих сверок подряд было
    /// </summary>
    public string Repeats { get; }

    /// <summary>
    /// Есть что сказать про число одинаковых сверок
    /// </summary>
    public bool HasRepeats => Repeats.Length > 0;

    /// <summary>
    /// Разбивает подробности записи на строки
    /// </summary>
    /// <param name="entry">Запись сверки</param>
    /// <returns>Непустые строки</returns>
    private static IReadOnlyList<string> Split(LogEntry entry) => [.. entry.Detail.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
}
