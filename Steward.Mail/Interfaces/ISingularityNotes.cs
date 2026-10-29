using Steward.Mail.Models;

namespace Steward.Mail.Interfaces;

/// <summary>
/// Чтение задач SingularityApp по внешнему ключу и дописывание заметок в них
/// </summary>
public interface ISingularityNotes
{
    /// <summary>
    /// Читает задачи с внешним ключом, которые начинаются позже заданного момента
    /// </summary>
    /// <param name="startsAfter">Нижняя граница начала задач</param>
    /// <param name="keyPrefix">Берутся только задачи, ключ которых начинается с этого текста</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задачи с ключом</returns>
    Task<IReadOnlyList<SingularityTaskInfo>> ListTasksAsync(DateTimeOffset startsAfter, string keyPrefix, CancellationToken cancellationToken);

    /// <summary>
    /// Дописывает текст в конец заметки задачи, не теряя написанного пользователем
    /// </summary>
    /// <param name="taskId">Номер задачи в SingularityApp</param>
    /// <param name="note">Дописываемый текст в формате Quill Delta</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    Task UpdateNoteAsync(string taskId, string note, CancellationToken cancellationToken);
}
