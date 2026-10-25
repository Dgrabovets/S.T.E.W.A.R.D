using System.Text.Json.Serialization;

namespace Steward.Mail.Models;

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
    /// Начало в формате ISO 8601 со смещением часового пояса, например <c>2026-10-25T00:00:00+01:00</c>. API отклоняет дату без времени и смещения
    /// </summary>
    [JsonPropertyName("start")]
    public string? Start { get; init; }

    /// <summary>
    /// Есть ли у задачи время. Задача из письма привязана ко дню, поэтому <see langword="false"/>
    /// </summary>
    [JsonPropertyName("useTime")]
    public bool UseTime { get; init; }

    /// <summary>
    /// Устойчивый ключ задачи: по нему видно, что письмо уже превращено в задачу, и повторно задача не создаётся
    /// </summary>
    [JsonPropertyName("externalId")]
    public string? ExternalId { get; init; }

    /// <summary>
    /// Проект назначения. Если не задан, поле не отправляется и задача попадает во «Входящие»
    /// </summary>
    [JsonPropertyName("projectId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectId { get; init; }
}
