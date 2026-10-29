namespace Steward.Homework.Models;

/// <summary>
/// Замечание владельца к подготовленной работе: учитывается при следующих подготовках
/// </summary>
/// <param name="Id">Уникальный ключ: <see cref="Guid"/> без дефисов</param>
/// <param name="Date">Когда замечание записано</param>
/// <param name="Subject">Предмет, при работе над которым оно появилось</param>
/// <param name="LessonId">Занятие, при работе над которым оно появилось</param>
/// <param name="Number">Номер задания в занятии</param>
/// <param name="Text">Текст замечания</param>
/// <param name="Kind">К чему относится замечание</param>
/// <param name="Global">Замечание общее: учитывается во всех предметах</param>
/// <param name="Enabled">Замечание учитывается. Выключенное остаётся в списке</param>
public sealed record Remark(string Id, DateOnly Date, string Subject, string LessonId, int Number, string Text, RemarkKindEnum Kind, bool Global, bool Enabled = true);
