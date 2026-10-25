using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Записывает созданные задачи и по желанию падает на одной из них
/// </summary>
public sealed class RecordingSingularity : ISingularityClient
{
    /// <summary>
    /// Задачи в порядке создания
    /// </summary>
    public List<SingularityTaskRequest> Created { get; } = [];

    /// <summary>
    /// Номер задачи (с единицы), на которой клиент бросает исключение. Ноль, если падать не нужно
    /// </summary>
    public int FailOnCall { get; init; }

    /// <summary>
    /// Записывает задачу или бросает исключение на заданном по счёту вызове
    /// </summary>
    /// <param name="request">Поля новой задачи</param>
    /// <param name="cancellationToken">Не используется</param>
    /// <returns>Завершённая задача</returns>
    /// <exception cref="HttpRequestException">Если это вызов, на котором нужно упасть</exception>
    public Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken)
    {
        if (Created.Count + 1 == FailOnCall)
            throw new HttpRequestException("Singularity недоступен");

        Created.Add(request);

        return Task.CompletedTask;
    }
}