using System.Text.Json.Serialization;

namespace Steward.Status.State;

/// <summary>
/// Состояние процесса в файле статуса. Незнакомое значение читается как <see cref="Unknown"/>, а не ломает чтение
/// </summary>
[JsonConverter(typeof(ProcessStateConverter))]
public enum ProcessStateEnum
{
    /// <summary>
    /// Значение не распознано: файл написан более новой версией формата
    /// </summary>
    Unknown,

    /// <summary>
    /// Процесс ничего не делает и ждёт своего времени
    /// </summary>
    Idle,

    /// <summary>
    /// Процесс выполняется прямо сейчас
    /// </summary>
    Running,

    /// <summary>
    /// Последний запуск закончился успешно
    /// </summary>
    Ok,

    /// <summary>
    /// Что-то готово и ждёт решения пользователя
    /// </summary>
    NeedsAttention,

    /// <summary>
    /// Последний запуск закончился ошибкой
    /// </summary>
    Failed,
}
