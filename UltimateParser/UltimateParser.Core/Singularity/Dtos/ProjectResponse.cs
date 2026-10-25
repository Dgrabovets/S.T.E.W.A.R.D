namespace UltimateParser.Singularity.Dtos;

/// <summary>
/// Поля проекта, которые читает клиент Singularity
/// </summary>
internal sealed class ProjectResponse
{
    /// <summary>
    /// Идентификатор проекта
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Название проекта
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Устойчивый ключ, по которому проект находится после переименования
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Идентификатор родительского проекта
    /// </summary>
    public string? Parent { get; set; }
}
