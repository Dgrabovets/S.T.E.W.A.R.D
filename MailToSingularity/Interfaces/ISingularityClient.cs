using MailToSingularity.Models;

namespace MailToSingularity.Interfaces;

/// <summary>
/// Клиент REST API SingularityApp
/// </summary>
public interface ISingularityClient
{
    /// <summary>
    /// Создаёт задачу
    /// </summary>
    /// <param name="request">
    /// Поля новой задачи
    /// </param>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда SingularityApp принимает запрос
    /// </returns>
    Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken ct);
}
