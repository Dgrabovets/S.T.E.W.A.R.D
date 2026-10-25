namespace Steward.Schedule.Singularity.Dtos;

/// <summary>
/// Тело <c>POST /v2/project</c>
/// </summary>
internal sealed class ProjectCreateResponse
{
    /// <summary>
    /// Созданный проект
    /// </summary>
    public ProjectResponse? Project { get; set; }
}
