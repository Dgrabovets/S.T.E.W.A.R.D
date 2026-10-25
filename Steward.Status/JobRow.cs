using System.Globalization;

namespace Steward.Status;

/// <summary>
/// Цвет строки задачи на панели
/// </summary>
public enum RowTone
{
    /// <summary>
    /// Задача ждёт, отключена или ещё не запускалась
    /// </summary>
    Idle,

    /// <summary>
    /// Последний запуск удался
    /// </summary>
    Ok,

    /// <summary>
    /// Задача выполняется сейчас
    /// </summary>
    Working,

    /// <summary>
    /// Готов результат, который ждёт решения пользователя
    /// </summary>
    Attention,

    /// <summary>
    /// Запуск не удался, завис или давно не было успешного запуска
    /// </summary>
    Problem,
}

/// <summary>
/// Готовая к показу строка задачи
/// </summary>
/// <param name="Id">Идентификатор задачи</param>
/// <param name="Title">Название задачи</param>
/// <param name="Headline">Короткий статус рядом с названием</param>
/// <param name="Detail">Подробности под названием</param>
/// <param name="Tone">Цвет строки</param>
/// <param name="Awaiting">Сколько результатов ждёт решения пользователя</param>
/// <param name="Enabled">Включена ли задача. Выключенная не влияет на общий статус</param>
public sealed record JobRow(string Id, string Title, string Headline, string Detail, RowTone Tone, int Awaiting, bool Enabled);

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
        var title = string.IsNullOrWhiteSpace(job.Title) ? job.Id : job.Title;
        var next = NextText(nextRun, now);

        JobRow Row(RowTone tone, string headline, string detail) => new(job.Id, title, headline, detail, tone, status?.Awaiting ?? 0, job.Enabled);

        if (!job.Enabled)
            return Row(RowTone.Idle, "Отключена", "Включается полем enabled в config/jobs.json");

        if (status is null)
            return Row(RowTone.Idle, "Ещё не запускалась", next);

        switch (status.State)
        {
            case ProcessState.Failed:
                return Row(RowTone.Problem, "Не удалось", $"{Short(status.Error)} ({When(status.UpdatedAt, now)})");
            case ProcessState.Running when now - status.UpdatedAt > job.Timeout + HangGrace:
                return Row(RowTone.Problem, "Завис?", $"Запущена {When(status.UpdatedAt, now)} и не отвечает");
            case ProcessState.Running:
                return Row(RowTone.Working, "Выполняется", $"Запущена {When(status.UpdatedAt, now)}");
        }

        var lastSuccess = status.LastSuccessAt ?? status.UpdatedAt;
        if (job.StaleAfterMinutes is { } stale && now - lastSuccess > TimeSpan.FromMinutes(stale))
            return Row(RowTone.Problem, "Давно не запускалась", $"Последний успех {When(lastSuccess, now)}");

        if (status.State == ProcessState.NeedsAttention || status.Awaiting > 0)
            return Row(RowTone.Attention, "Ждёт тебя", Join(status.Summary, status.Awaiting > 0 ? $"Ждут решения: {status.Awaiting}" : null));

        return status.State switch
        {
            ProcessState.Ok => Row(RowTone.Ok, $"Выполнено {When(lastSuccess, now)}", Join(status.Summary, next)),
            ProcessState.Idle => Row(RowTone.Idle, "Ожидает", Join(status.Summary, next)),
            _ => Row(RowTone.Idle, "Статус не распознан", "Файл статуса записан более новой версией формата"),
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
    /// Говорит о моменте словами: сегодня, вчера или датой
    /// </summary>
    /// <param name="time">Момент события</param>
    /// <param name="now">Текущий момент</param>
    /// <returns>Фраза вида «сегодня в 09:00»</returns>
    private static string When(DateTimeOffset time, DateTimeOffset now)
    {
        var local = time.ToOffset(now.Offset);
        var clock = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == now.Date)
            return $"сегодня в {clock}";
        if (local.Date == now.Date.AddDays(-1))
            return $"вчера в {clock}";

        return $"{local:dd.MM} в {clock}";
    }

    /// <summary>
    /// Говорит, когда задача запустится в следующий раз
    /// </summary>
    /// <param name="next">
    /// Время следующего запуска или <see langword="null"/>
    /// </param>
    /// <param name="now">Текущий момент</param>
    /// <returns>Фраза вида «Следующий запуск завтра в 09:00» или пустая строка, если запуска по времени нет</returns>
    private static string NextText(DateTimeOffset? next, DateTimeOffset now)
    {
        if (next is null)
            return string.Empty;

        var local = next.Value.ToOffset(now.Offset);
        var clock = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        if (local.Date == now.Date)
            return $"Следующий запуск сегодня в {clock}";
        if (local.Date == now.Date.AddDays(1))
            return $"Следующий запуск завтра в {clock}";

        return $"Следующий запуск {local:dd.MM} в {clock}";
    }

    #endregion Wording
}
