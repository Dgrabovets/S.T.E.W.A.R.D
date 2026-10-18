namespace UltimateParser.Tray.Models;

/// <summary>
/// Цвет плашки состояния на панели
/// </summary>
public enum PanelTone
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
    /// Последняя сверка закончилась сбоем
    /// </summary>
    Problem,
}
