using UltimateParser.Common.Models;
using UltimateParser.Singularity.Models;

namespace UltimateParser.Singularity.Interfaces;

/// <summary>
/// Создаёт и обновляет проект Singularity, в котором лежит расписание
/// </summary>
public interface ISingularityClient
{
    /// <summary>
    /// Возвращает идентификатор проекта группы и создаёт его, если такого нет. Сначала проект ищется по устойчивому ключу, затем по названию
    /// </summary>
    /// <param name="title">
    /// Точное название проекта. По нему проект находится, пока на нём нет ключа, и с ним создаётся новый
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор проекта, начинается с <c>P-</c>
    /// </returns>
    Task<string> GetOrCreateProjectIdAsync(string title, CancellationToken cancellationToken);

    /// <summary>
    /// Ищет активный проект по точному названию
    /// </summary>
    /// <param name="title">
    /// Точное название проекта
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор проекта или <see langword="null"/>, если проекта нет
    /// </returns>
    Task<string?> FindProjectIdAsync(string title, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает идентификатор тега с названием <paramref name="title"/> и создаёт тег, если такого нет
    /// </summary>
    /// <param name="title">
    /// Название тега. Регистр не важен
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор тега, начинается с <c>A-</c>
    /// </returns>
    Task<string> GetOrCreateTagIdAsync(string title, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает задачи проекта, включая архивные
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задачи, доступные токену. Удалённые задачи не запрашиваются
    /// </returns>
    Task<IReadOnlyList<SingularityTask>> ListProjectTasksAsync(
        string projectId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Создаёт задачу для занятия
    /// </summary>
    /// <param name="projectId">
    /// Проект назначения
    /// </param>
    /// <param name="lesson">
    /// Занятие, которое нужно создать
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Идентификатор созданной задачи
    /// </returns>
    Task<string> CreateTaskAsync(string projectId, Lesson lesson, CancellationToken cancellationToken);

    /// <summary>
    /// Обновляет название, заметку, начало и длительность существующей задачи
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="projectId">
    /// Проект, в котором задача должна остаться
    /// </param>
    /// <param name="lesson">
    /// Желаемые значения занятия
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда Singularity принимает изменение
    /// </returns>
    Task UpdateTaskAsync(
        string taskId,
        string projectId,
        Lesson lesson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Отправляет задачу в архив
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="journalDate">
    /// Момент архивации со смещением часового пояса
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается после архивации
    /// </returns>
    Task ArchiveTaskAsync(string taskId, DateTimeOffset journalDate, CancellationToken cancellationToken);

    /// <summary>
    /// Отмечает задачу выполненной
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API принимает отметку
    /// </returns>
    Task CompleteTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает архивную задачу в активный список
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда задача снова активна
    /// </returns>
    Task UnarchiveTaskAsync(string taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет задачу
    /// </summary>
    /// <param name="taskId">
    /// Идентификатор задачи
    /// </param>
    /// <param name="cancellationToken">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда API удаляет задачу
    /// </returns>
    Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken);
}
