namespace UltimateParser.Singularity.Dtos;

/// <summary>
/// Поля тега, которые читает клиент Singularity
/// </summary>
internal sealed class TagResponse
{
    /// <summary>
    /// Идентификатор тега
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Название тега
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Признак того, что тег в корзине
    /// </summary>
    public bool? Removed { get; set; }
}
