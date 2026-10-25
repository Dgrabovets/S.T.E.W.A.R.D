namespace Steward.Schedule.Singularity.Dtos;

/// <summary>
/// Страница задач из <c>GET /v2/task</c>
/// </summary>
internal sealed class TaskListResponse
{
    /// <summary>
    /// Задачи на этой странице
    /// </summary>
    public List<TaskResponse>? Tasks { get; set; }
}
