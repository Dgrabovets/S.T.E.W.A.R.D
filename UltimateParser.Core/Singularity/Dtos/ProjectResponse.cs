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
}
