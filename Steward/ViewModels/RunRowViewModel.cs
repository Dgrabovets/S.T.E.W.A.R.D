using System.Globalization;
using Steward.Models;
using Steward.Mvvm;
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
    /// <param name="fix">Вызывается, когда человек просит починить запуск</param>
    public RunRowViewModel(RunRecord record, Action<RunRecord> fix)
    {
        FixCommand = new RelayCommand(() => fix(record));
        CanFix = record.Outcome != RunOutcomeEnum.Ok;
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
        Summary = record.Summary ?? string.Empty;
        Output = record.Output ?? string.Empty;
        OutputLines = LogLines.Parse(Output);
        Facts = string.Join(" · ", new[] { When, Reason, Duration(record.FinishedAt - record.StartedAt), record.ExitCode is { } code and not 0 ? $"код выхода {code}" : null }.Where(part => part is not null));
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
    public bool HasDetail => Detail.Length > 0 && !InOutput(Detail);

    /// <summary>
    /// Что запуск сделал: сводка процесса или раннера
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Просит Claude починить неудавшийся запуск
    /// </summary>
    public RelayCommand FixCommand { get; }

    /// <summary>
    /// Запуск не удался, значит, его можно отдать на починку
    /// </summary>
    public bool CanFix { get; }

    /// <summary>
    /// Есть сводка
    /// </summary>
    public bool HasSummary => Summary.Length > 0 && !InOutput(Summary);

    /// <summary>
    /// Хвост вывода процесса
    /// </summary>
    public string Output { get; }

    /// <summary>
    /// У запуска есть вывод
    /// </summary>
    public bool HasOutput => Output.Length > 0;

    /// <summary>
    /// Вывод по записям: каждая показывается своей плашкой
    /// </summary>
    public IReadOnlyList<LogLine> OutputLines { get; }

    /// <summary>
    /// Строка фактов: когда, почему, сколько шёл и каким кодом закончился
    /// </summary>
    public string Facts { get; }

    /// <summary>
    /// Цвет точки
    /// </summary>
    public PanelToneEnum Tone { get; }

    /// <summary>
    /// Проверяет, что текст уже есть в плашках вывода и над ними его повторять не нужно. У ошибки сравнивается сообщение без названия типа исключения
    /// </summary>
    /// <param name="text">Ошибка или сводка запуска</param>
    /// <returns>
    /// <see langword="true"/>, если вывод содержит этот текст
    /// </returns>
    private bool InOutput(string text)
    {
        var separator = text.IndexOf(": ", StringComparison.Ordinal);
        var message = separator >= 0 ? text[(separator + 2)..] : text;

        return Output.Contains(text, StringComparison.Ordinal) || Output.Contains(message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Пишет длительность коротко: «4 с», «2 мин 5 с», «1 ч 3 мин»
    /// </summary>
    /// <param name="span">Сколько шёл запуск</param>
    /// <returns>Текст длительности</returns>
    private static string Duration(TimeSpan span) => span.TotalSeconds switch
    {
        < 1 => "меньше секунды",
        < 60 => $"{(int)span.TotalSeconds} с",
        < 3600 => $"{(int)span.TotalMinutes} мин {span.Seconds} с",
        _ => $"{(int)span.TotalHours} ч {span.Minutes} мин",
    };
}
