namespace UltimateParser.Singularity.Dtos;

/// <summary>
/// Страница тегов из <c>GET /v2/tag</c>
/// </summary>
internal sealed class TagListResponse
{
    /// <summary>
    /// Теги на этой странице
    /// </summary>
    public List<TagResponse>? Tags { get; set; }
}
