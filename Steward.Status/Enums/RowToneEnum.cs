namespace Steward.Status.Rows;

/// <summary>
/// Цвет строки задачи на панели
/// </summary>
public enum RowToneEnum
{
    /// <summary>
    /// Задача ждёт, отключена или ещё не запускалась
    /// </summary>
    Idle,

    /// <summary>
    /// Последний запуск удался
    /// </summary>
    Ok,

    /// <summary>
    /// Задача выполняется сейчас
    /// </summary>
    Working,

    /// <summary>
    /// Готов результат, который ждёт решения пользователя
    /// </summary>
    Attention,

    /// <summary>
    /// Запуск не удался, завис или давно не было успешного запуска
    /// </summary>
    Problem,
}
