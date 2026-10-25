namespace Steward.Mail.Common;

/// <summary>
/// Правила разбора писем
/// </summary>
public sealed class ClassifierSettings
{
    /// <summary>
    /// Системные адреса, письма с которых во «Входящих» не превращаются в задачи
    /// </summary>
    public List<string> IgnoredSenders { get; init; } = [];
}
