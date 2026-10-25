using Steward.Status.Common;
using Steward.Status.Registry;
using Steward.Status.State;

namespace Steward.Status.Rows;

/// <summary>
/// Собирает строку задачи из её описания и файла статуса
/// </summary>
public static class JobRowBuilder
{
    /// <summary>
    /// Длина текста ошибки, которая ещё читается на панели
    /// </summary>
    private const int ERROR_LIMIT = 160;

    /// <summary>
    /// Запас сверх таймаута, после которого «выполняется» считается зависанием
    /// </summary>
    private static readonly TimeSpan HangGrace = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Сводит описание задачи и статус в строку для показа
    /// </summary>
    /// <param name="job">Описание задачи из реестра</param>
    /// <param name="status">
    /// Файл статуса или <see langword="null"/>, если задача ещё не запускалась
    /// </param>
    /// <param name="nextRun">
    /// Время следующего запуска по расписанию или <see langword="null"/>
    /// </param>
    /// <param name="now">Текущий момент. Дни «сегодня» и «вчера» считаются в его часовом поясе</param>
    /// <returns>Строка с цветом, статусом и подробностями</returns>
    public static JobRow Build(JobDefinition job, ProcessStatus? status, DateTimeOffset? nextRun, DateTimeOffset now)
    {
        var next = NextText(nextRun, now);

        JobRow Row(RowToneEnum tone, string headline, string detail) => new(job.Id, job.DisplayTitle, headline, detail, tone, status?.Awaiting ?? 0, job.Enabled);

        if (!job.Enabled)
            return Row(RowToneEnum.Idle, "Отключена", "Включается полем enabled в config/jobs.json");

        if (status is null)
            return Row(RowToneEnum.Idle, "Ещё не запускалась", next);

        switch (status.State)
        {
            case ProcessStateEnum.Failed:
                return Row(RowToneEnum.Problem, "Не удалось", $"{Short(status.Error)} ({TimeWording.Moment(status.UpdatedAt, now)})");
            case ProcessStateEnum.Running when now - status.UpdatedAt > job.Timeout + HangGrace:
                return Row(RowToneEnum.Problem, "Завис?", $"Запущена {TimeWording.Moment(status.UpdatedAt, now)} и не отвечает");
            case ProcessStateEnum.Running:
                return Row(RowToneEnum.Working, "Выполняется", $"Запущена {TimeWording.Moment(status.UpdatedAt, now)}");
        }

        var lastSuccess = status.LastSuccessAt ?? status.UpdatedAt;
        if (job.StaleAfterMinutes is { } stale && now - lastSuccess > TimeSpan.FromMinutes(stale))
            return Row(RowToneEnum.Problem, "Давно не запускалась", $"Последний успех {TimeWording.Moment(lastSuccess, now)}");

        if (status.State == ProcessStateEnum.NeedsAttention || status.Awaiting > 0)
            return Row(RowToneEnum.Attention, "Ждёт тебя", Join(status.Summary, status.Awaiting > 0 ? $"Ждут решения: {status.Awaiting}" : null));

        return status.State switch
        {
            ProcessStateEnum.Ok => Row(RowToneEnum.Ok, $"Выполнено {TimeWording.Moment(lastSuccess, now)}", Join(status.Summary, next)),
            ProcessStateEnum.Idle => Row(RowToneEnum.Idle, "Ожидает", Join(status.Summary, next)),
            _ => Row(RowToneEnum.Idle, "Статус не распознан", "Файл статуса записан более новой версией формата"),
        };
    }

    #region Wording

    /// <summary>
    /// Склеивает непустые части через точку
    /// </summary>
    /// <param name="parts">Фразы, часть из которых может быть пустой</param>
    /// <returns>Одна строка</returns>
    private static string Join(params string?[] parts) =>
        string.Join(". ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim().TrimEnd('.')));

    /// <summary>
    /// Обрезает текст ошибки до читаемой длины
    /// </summary>
    /// <param name="error">
    /// Сообщение или <see langword="null"/>
    /// </param>
    /// <returns>Короткий текст</returns>
    private static string Short(string? error)
    {
        var text = string.IsNullOrWhiteSpace(error) ? "Причина не записана" : error.Trim();
        return text.Length <= ERROR_LIMIT ? text : string.Concat(text.AsSpan(0, ERROR_LIMIT).Trim(), "…");
    }

    /// <summary>
    /// Говорит, когда задача запустится в следующий раз
    /// </summary>
    /// <param name="next">
    /// Время следующего запуска или <see langword="null"/>
    /// </param>
    /// <param name="now">Текущий момент</param>
    /// <returns>Фраза вида «Следующий запуск завтра в 09:00» или пустая строка, если запуска по времени нет</returns>
    private static string NextText(DateTimeOffset? next, DateTimeOffset now) => next is null ? string.Empty : $"Следующий запуск {TimeWording.Moment(next.Value, now)}";

    #endregion Wording
}
