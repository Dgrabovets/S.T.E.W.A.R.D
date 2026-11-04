namespace Steward.Models;

/// <summary>
/// Что уже сделано с заданием из разбора занятия
/// </summary>
/// <param name="Listed">Задание лежит в списке заданий занятия, в том числе в архиве</param>
/// <param name="InSingularity">По заданию заведена задача в Singularity</param>
/// <param name="Dismissed">Владелец убрал задание из разбора</param>
public readonly record struct AssignmentState(bool Listed, bool InSingularity, bool Dismissed)
{
    /// <summary>
    /// Владелец ещё никак не отреагировал на задание: его нет в списке, нет в Singularity и оно не убрано
    /// </summary>
    public bool IsOpen => !Listed && !InSingularity && !Dismissed;
}
