namespace Steward.Models;

/// <summary>
/// Как сфера показывает, что процесс закончился
/// </summary>
public enum ActivityDoneStyleEnum
{
    /// <summary>
    /// Сфера вспыхивает, от неё расходится кольцо из точек и тает
    /// </summary>
    Ripple,

    /// <summary>
    /// Точки собираются в кольцо с галочкой из точек, держатся и снова становятся сферой
    /// </summary>
    DotTick,

    /// <summary>
    /// Сфера схлопывается в яркую точку и взрывается, точки возвращаются на места
    /// </summary>
    Burst,
}