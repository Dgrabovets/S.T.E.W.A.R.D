using Steward.Mail.Models;

namespace Steward.Mail.Interfaces;

/// <summary>
/// Клиент REST API SingularityApp
/// </summary>
public interface ISingularityClient
{
    /// <summary>
    /// Создаёт задачу
    /// </summary>
    /// <param name="request">Поля новой задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Собирает внешние ключи уже созданных задач, включая выполненные, архивные и удалённые
    /// </summary>
    /// <param name="startsAfter">Задачи, которые начинаются позже этого момента. Раньше задачи из писем не создаются</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Внешние ключи без повторов</returns>
    Task<IReadOnlySet<string>> ListExternalIdsAsync(DateTimeOffset startsAfter, CancellationToken cancellationToken);
}
