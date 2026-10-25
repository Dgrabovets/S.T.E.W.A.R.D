using System.Text.Json.Serialization;

namespace MailToSingularity.Models;

/// <summary>
/// Тело запроса POST /v2/task
/// </summary>
public sealed class SingularityTaskRequest
{
    /// <summary>
    /// Название задачи
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Заметка к задаче: отправитель, тема, дата, начало письма и ссылка на почту
    /// </summary>
    [JsonPropertyName("note")]
    public string? Note { get; init; }

    /// <summary>
    /// Приоритет: 0 — высокий, 1 — обычный, 2 — низкий
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 1;

    /// <summary>
    /// Дата начала в формате ISO 8601
    /// </summary>
    [JsonPropertyName("start")]
    public string? Start { get; init; }

    /// <summary>
    /// Проект назначения. Если не задан, поле не отправляется и задача попадает во «Входящие»
    /// </summary>
    [JsonPropertyName("projectId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectId { get; init; }
}
