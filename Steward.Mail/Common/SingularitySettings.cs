namespace Steward.Mail.Common;

/// <summary>
/// Параметры REST API SingularityApp
/// </summary>
public sealed class SingularitySettings
{
    /// <summary>
    /// Адрес API без завершающего слэша
    /// </summary>
    public string BaseUrl { get; init; } = "https://api.singularity-app.com";

    /// <summary>
    /// Токен доступа к API. Создаётся в личном кабинете SingularityApp
    /// </summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Идентификатор проекта для задач
    /// Если не задан, задачи попадают во «Входящие»
    /// </summary>
    public string? ProjectId { get; init; }
}
