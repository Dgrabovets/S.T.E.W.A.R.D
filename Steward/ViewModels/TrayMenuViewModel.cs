using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Status.Rows;

namespace Steward.ViewModels;

/// <summary>
/// Меню у иконки: общий статус, быстрый список задач и кнопки из капель
/// </summary>
public sealed class TrayMenuViewModel : PanelViewModel
{
    /// <summary>
    /// Следит за выполняющимися и только что закончившимися задачами
    /// </summary>
    private readonly ActivityTracker _activity = new();

    /// <summary>
    /// Карточки задач для быстрого статуса
    /// </summary>
    private IReadOnlyList<TaskCardViewModel> _tasks = [];

    /// <summary>
    /// Создаёт модель с подписью «Проверяю задачи», пока нет данных
    /// </summary>
    public TrayMenuViewModel()
        : base("Проверяю задачи")
    {
    }

    /// <summary>
    /// Карточки задач для быстрого статуса
    /// </summary>
    public IReadOnlyList<TaskCardViewModel> Tasks
    {
        get => _tasks;
        private set => Set(ref _tasks, value);
    }

    /// <summary>
    /// Показывает свежие данные
    /// </summary>
    /// <param name="snapshot">Состояние контейнера расписания</param>
    /// <param name="jobs">Строки задач из реестра</param>
    public void Apply(SyncSnapshot snapshot, IReadOnlyList<JobRow> jobs)
    {
        var rows = TaskRows.Build(snapshot, jobs);
        _activity.Track(rows, DateTimeOffset.UtcNow);
        Tasks = [.. rows.Select(row => new TaskCardViewModel(row, _ => { }, _activity.StateOf(row), _activity.FinishedAt(row.Id)))];

        ApplySummary(rows);
    }
}