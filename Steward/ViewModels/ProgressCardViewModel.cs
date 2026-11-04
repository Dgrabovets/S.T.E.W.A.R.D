using Steward.Status.Progress;

namespace Steward.ViewModels;

/// <summary>
/// Блок «Что сейчас выполняется» на карточке процесса: шаги запуска, строка с пояснением, полоса и время работы
/// </summary>
public sealed class ProgressCardViewModel
{
    /// <summary>
    /// Сколько минут прогресс считается свежим: процесс, который дольше не обновлял файл, считается оборвавшимся
    /// </summary>
    public const int FRESH_MINUTES = 40;

    /// <summary>
    /// Создаёт блок по файлу прогресса
    /// </summary>
    /// <param name="progress">Прогресс запуска</param>
    /// <param name="now">Текущее время: от него считается, сколько идёт запуск</param>
    public ProgressCardViewModel(RunProgress progress, DateTimeOffset now)
    {
        Steps = [.. progress.Stages.Select((title, index) => new PipelineStepViewModel(title, index == progress.Current, index < progress.Current, index == 0))];
        Line = LineOf(progress);
        Share = Math.Clamp((progress.Current + progress.StageShare) / Math.Max(progress.Stages.Count, 1), 0, 1);
        Elapsed = "идёт " + ElapsedWording(now - progress.Started);
    }

    /// <summary>
    /// Шаги запуска по порядку, текущий выделен
    /// </summary>
    public IReadOnlyList<PipelineStepViewModel> Steps { get; }

    /// <summary>
    /// Главная строка: шаг, счётчик и что обрабатывается, например «Разбор: занятие 2 из 3, Гейм-дизайн, 25.10.2026»
    /// </summary>
    public string Line { get; }

    /// <summary>
    /// Доля всего запуска от нуля до единицы: пройденные шаги плюс доля текущего
    /// </summary>
    public double Share { get; }

    /// <summary>
    /// Сколько идёт запуск, например «идёт 4 мин»
    /// </summary>
    public string Elapsed { get; }

    /// <summary>
    /// Подпись всей полосы для экранного диктора
    /// </summary>
    public string AccessibleName => $"{Line}. {Elapsed}";

    /// <summary>
    /// Собирает блок, если процесс правда работает и файл свежий
    /// </summary>
    /// <param name="progress">Прогресс из файла или <see langword="null"/>, если файла нет</param>
    /// <param name="working">Процесс сейчас выполняется по данным раннера</param>
    /// <param name="now">Текущее время</param>
    /// <returns>Блок или <see langword="null"/>, если показывать нечего</returns>
    public static ProgressCardViewModel? Create(RunProgress? progress, bool working, DateTimeOffset now) =>
        progress is not null && working && now - progress.Updated <= TimeSpan.FromMinutes(FRESH_MINUTES) ? new ProgressCardViewModel(progress, now) : null;

    /// <summary>
    /// Собирает главную строку
    /// </summary>
    /// <param name="progress">Прогресс запуска</param>
    /// <returns>Строка с названием шага, счётчиком и пояснением</returns>
    private static string LineOf(RunProgress progress)
    {
        var title = progress.CurrentTitle;
        var counter = progress.Total > 0 ? $": {Math.Min(progress.Done + 1, progress.Total)} из {progress.Total}" : string.Empty;
        var detail = progress.Detail.Length > 0 ? ", " + progress.Detail : string.Empty;

        return title.Length == 0 ? progress.Detail : $"{title}{counter}{detail}";
    }

    /// <summary>
    /// Пишет, сколько прошло времени, по-русски: «меньше минуты», «4 мин», «1 ч 05 мин»
    /// </summary>
    /// <param name="span">Сколько прошло</param>
    /// <returns>Короткая запись</returns>
    private static string ElapsedWording(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
            return "меньше минуты";

        return span.TotalHours >= 1 ? $"{(int)span.TotalHours} ч {span.Minutes:00} мин" : $"{(int)span.TotalMinutes} мин";
    }
}
