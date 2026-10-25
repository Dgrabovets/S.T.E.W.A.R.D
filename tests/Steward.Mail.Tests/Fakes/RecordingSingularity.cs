using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Tests.Fakes;

/// <summary>
/// Записывает созданные задачи, помнит их внешние ключи и по желанию падает на одной из них
/// </summary>
public sealed class RecordingSingularity : ISingularityClient
{
    /// <summary>
    /// Задачи в порядке создания
    /// </summary>
    public List<SingularityTaskRequest> Created { get; } = [];

    /// <summary>
    /// Внешние ключи задач, которые уже есть в SingularityApp: заданные заранее и созданные в этом прогоне
    /// </summary>
    public HashSet<string> Existing { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Номер задачи (с единицы), на которой клиент бросает исключение. Ноль, если падать не нужно
    /// </summary>
    public int FailOnCall { get; init; }

    /// <summary>
    /// С какой границы начала запрашивали задачи в последний раз
    /// </summary>
    public DateTimeOffset? LastStartsAfter { get; private set; }

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
        Existing.Add(request.ExternalId!);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Отдаёт ключи задач, которые уже есть
    /// </summary>
    /// <param name="startsAfter">Запоминается, чтобы тест проверил окно поиска</param>
    /// <param name="cancellationToken">Не используется</param>
    /// <returns>Копия набора ключей</returns>
    public Task<IReadOnlySet<string>> ListExternalIdsAsync(DateTimeOffset startsAfter, CancellationToken cancellationToken)
    {
        LastStartsAfter = startsAfter;

        return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(Existing, StringComparer.Ordinal));
    }
}