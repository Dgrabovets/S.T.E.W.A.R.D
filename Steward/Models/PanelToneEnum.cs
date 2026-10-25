namespace Steward.Models;

/// <summary>
/// Цвет плашки состояния на панели
/// </summary>
public enum PanelToneEnum
{
    /// <summary>
    /// Процесс остановлен или Docker не отвечает
    /// </summary>
    Idle,

    /// <summary>
    /// Контейнер запущен и последняя сверка прошла
    /// </summary>
    Running,

    /// <summary>
    /// Готов результат, который ждёт решения пользователя
    /// </summary>
    Attention,

    /// <summary>
    /// Последняя сверка закончилась сбоем
    /// </summary>
    Problem,
}
