namespace Steward.Status.Running;

/// <summary>
/// Чем закончился запуск
/// </summary>
public enum RunOutcomeEnum
{
    /// <summary>
    /// Программа завершилась с кодом 0
    /// </summary>
    Ok,

    /// <summary>
    /// Программа завершилась с ошибкой
    /// </summary>
    Failed,

    /// <summary>
    /// Программа не уложилась во время и была остановлена
    /// </summary>
    TimedOut,

    /// <summary>
    /// Программа не запустилась: нет файла или нет прав
    /// </summary>
    NotStarted,
}
