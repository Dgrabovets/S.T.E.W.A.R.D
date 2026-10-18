namespace UltimateParser.Singularity.Dtos;

/// <summary>
/// Страница проектов из <c>GET /v2/project</c>
/// </summary>
internal sealed class ProjectListResponse
{
    /// <summary>
    /// Проекты на этой странице
    /// </summary>
    public List<ProjectResponse>? Projects { get; set; }
}
