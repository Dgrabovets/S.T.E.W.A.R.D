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
    /// <param name="remove">Вызывается, когда человек удаляет запись из журнала</param>
    /// <param name="same">
    /// Все подряд идущие запуски с тем же итогом и тем же выводом от новых к старым, считая саму запись, или <see langword="null"/>, если запуск один. Показываются одной строкой, удаляются вместе
    /// </param>
    public RunRowViewModel(RunRecord record, Action<RunRecord> fix, Action<RunRecord>? remove = null, IReadOnlyList<RunRecord>? same = null)
    {
        var all = same is { Count: > 0 } ? same : [record];
        FixCommand = new RelayCommand(() => fix(record));
        RemoveCommand = new RelayCommand(() =>
        {
            foreach (var item in all)
                remove?.Invoke(item);
        });
        Repeats = JournalWording.Repeats(all.Count);
        When = record.StartedAt.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
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
        CanFix = record.Outcome != RunOutcomeEnum.Ok || OutputLines.Any(line => line.Tone == PanelToneEnum.Problem);
        IsManual = record.Reason == "manual";
        Facts = Duration(record.FinishedAt - record.StartedAt);
        (Tone, Kind) = record.Outcome switch
        {
            RunOutcomeEnum.Ok => (PanelToneEnum.Running, ActivityKindEnum.Sealed),
            RunOutcomeEnum.Failed => (PanelToneEnum.Problem, ActivityKindEnum.Shattered),
            RunOutcomeEnum.TimedOut => (PanelToneEnum.Attention, ActivityKindEnum.Sweep),
            _ => (PanelToneEnum.Idle, ActivityKindEnum.Hollow),
        };
    }

    /// <summary>
    /// Вид сферы запуска: глобус у удачного, осколки у ошибки, часовая стрелка у превышения времени, кольцо у несостоявшегося
    /// </summary>
    public ActivityKindEnum Kind { get; }

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
    /// Удаляет эту запись из журнала
    /// </summary>
    public RelayCommand RemoveCommand { get; }

    /// <summary>
    /// Запуск не удался или прошёл с ошибками в выводе (например, не вышло разобрать одну из ссылок), значит, его можно отдать на починку
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
    /// Запуск начал человек: такой запуск выделяется среди запусков по расписанию
    /// </summary>
    public bool IsManual { get; }

    /// <summary>
    /// Сколько шёл запуск
    /// </summary>
    public string Facts { get; }

    /// <summary>
    /// Сколько таких же запусков шло подряд. Пусто, если запуск один
    /// </summary>
    public string Repeats { get; }

    /// <summary>
    /// Запусков подряд было несколько
    /// </summary>
    public bool HasRepeats => Repeats.Length > 0;

    /// <summary>
    /// Ключ, по которому подряд идущие запуски считаются одинаковыми: итог, сводка, причина и вывод без отметок времени
    /// </summary>
    /// <param name="record">Запись о запуске</param>
    /// <returns>Строка для сравнения</returns>
    public static string SameKey(RunRecord record) => string.Join('\u001F', record.Outcome, record.Summary, record.Detail, string.Join('\n', LogLines.Parse(record.Output ?? string.Empty).Select(line => line.Text)));

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
        < 60 => $"{(int)span.TotalSeconds} с.",
        < 3600 => $"{(int)span.TotalMinutes} мин. {span.Seconds} с.",
        _ => $"{(int)span.TotalHours} ч. {span.Minutes} мин.",
    };
}
