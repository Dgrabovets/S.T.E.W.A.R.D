namespace Steward.Mail.Common;

/// <summary>
/// Корневые настройки приложения
/// Загружаются из appsettings.json рядом с exe
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Подключение к почтовому ящику
    /// </summary>
    public MailSettings Mail { get; init; } = new();

    /// <summary>
    /// Подключение к SingularityApp
    /// </summary>
    public SingularitySettings Singularity { get; init; } = new();

    /// <summary>
    /// Правила разбора писем
    /// </summary>
    public ClassifierSettings Classifier { get; init; } = new();
}
