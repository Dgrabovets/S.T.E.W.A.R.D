namespace Steward.Models;

/// <summary>
/// Что показывает индикатор процесса
/// </summary>
public enum ActivityStateEnum
{
    /// <summary>
    /// Ничего не происходит: обычная точка состояния
    /// </summary>
    None,

    /// <summary>
    /// Процесс идёт: вращающаяся сфера из точек
    /// </summary>
    Working,

    /// <summary>
    /// Процесс только что закончился: рисуется галочка, через несколько секунд снова точка
    /// </summary>
    Done,
}