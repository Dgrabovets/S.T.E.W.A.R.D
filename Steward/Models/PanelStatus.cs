using System.Globalization;
using Steward.Monitoring.Models;

namespace Steward.Models;

/// <summary>
/// Тексты шапки и плашки, собранные из снимка контейнера
/// </summary>
public sealed class PanelStatus
{
    /// <summary>
    /// Собирает тексты из уже посчитанных фраз
    /// </summary>
    /// <param name="subtitle">Строка под заголовком</param>
    /// <param name="summary">Фраза о текущем состоянии</param>
    /// <param name="label">Короткий текст плашки</param>
    /// <param name="tone">Цвет плашки</param>
    /// <param name="emptyText">Строка вместо пустого журнала</param>
    /// <param name="cards">Карточки журнала</param>
    private PanelStatus(string subtitle,
                        string summary,
                        string label,
                        PanelToneEnum tone,
                        string emptyText,
                        IReadOnlyList<JournalCard> cards)
    {
        Subtitle = subtitle;
        Summary = summary;
        Label = label;
        Tone = tone;
        EmptyText = emptyText;
        Cards = cards;
    }

    /// <summary>
    /// Строка под заголовком «Расписание»
    /// </summary>
    public string Subtitle { get; }

    /// <summary>
    /// Одна фраза о том, что происходит сейчас
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Текст цветной плашки
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Какой цвет взять для плашки
    /// </summary>
    public PanelToneEnum Tone { get; }

    /// <summary>
    /// Что показать, если карточек нет
    /// </summary>
    public string EmptyText { get; }

    /// <summary>
    /// Карточки журнала от новых к старым
    /// </summary>
    public IReadOnlyList<JournalCard> Cards { get; }

    #region Snapshot

    /// <summary>
    /// Собирает тексты панели из снимка
    /// </summary>
    /// <param name="snapshot">Текущее состояние синхронизации</param>
    /// <returns>Фразы и карточки для окна</returns>
    public static PanelStatus From(SyncSnapshot snapshot)
    {
        var tone = snapshot.CurrentError is not null
            ? PanelToneEnum.Problem
            : snapshot.ContainerRunning ? PanelToneEnum.Running : PanelToneEnum.Idle;
        return new PanelStatus(
            When(snapshot.LastSuccess),
            SummaryOf(snapshot),
            LabelOf(snapshot),
            tone,
            snapshot.DockerAvailable ? "Журнал пока пуст" : "Docker не запущен",
            JournalCard.From(snapshot.Entries));
    }

    #endregion Snapshot

    #region Wording

    /// <summary>
    /// Короткая подпись состояния
    /// </summary>
    /// <param name="snapshot">Текущий снимок</param>
    /// <returns>Текст плашки</returns>
    private static string LabelOf(SyncSnapshot snapshot)
    {
        if (snapshot.CurrentError is not null)
            return "Ошибка";
        if (!snapshot.DockerAvailable)
            return "Нет Docker";

        return snapshot.ContainerRunning ? "Работает" : "Остановлено";
    }

    /// <summary>
    /// Одной фразой говорит, что сейчас происходит
    /// </summary>
    /// <param name="snapshot">Текущий снимок</param>
    /// <returns>Строка под временем обновления</returns>
    private static string SummaryOf(SyncSnapshot snapshot)
    {
        if (!snapshot.DockerAvailable)
            return "Docker не запущен, сверка с таблицей не идёт";
        if (!snapshot.ContainerRunning)
            return "Контейнер остановлен, новые сверки не запускаются";
        if (snapshot.CurrentError is not null)
            return "Последняя сверка не удалась";
        if (snapshot.Entries.Count == 0)
            return "Синхронизация запущена, первая сверка ещё впереди";

        var latest = snapshot.Entries[0];
        if (latest.Tone == LogToneEnum.Update)
            return "Последняя сверка внесла правки в задачи";
        if (latest.Repeat > 1)
            return "Несколько сверок подряд ничего не меняли";

        return "Последняя сверка ничего не меняла";
    }

    /// <summary>
    /// Формулирует время последнего успешного прогона
    /// </summary>
    /// <param name="lastSuccess">Местное время прогона. Пусто, если его ещё не было</param>
    /// <returns>Строка под заголовком</returns>
    private static string When(DateTimeOffset? lastSuccess)
    {
        if (lastSuccess is null)
            return "Ещё не обновлялось";

        var local = lastSuccess.Value;
        var clock = local.ToString("HH:mm", CultureInfo.InvariantCulture);
        var day = local.Date;
        if (day == DateTime.Today)
            return $"Обновлено сегодня в {clock}";
        if (day == DateTime.Today.AddDays(-1))
            return $"Обновлено вчера в {clock}";

        return $"Обновлено {local:dd.MM} в {clock}";
    }

    #endregion Wording
}
