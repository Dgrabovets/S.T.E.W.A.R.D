namespace Steward.Status.Registry;

/// <summary>
/// Результат чтения списка задач
/// </summary>
/// <param name="Jobs">Задачи, прошедшие проверку</param>
/// <param name="Errors">Что не удалось прочитать и почему. Пусто, если всё в порядке</param>
public sealed record JobRegistryResult(IReadOnlyList<JobDefinition> Jobs, IReadOnlyList<string> Errors);
