namespace Steward.Homework.Models;

/// <summary>
/// Одно найденное задание вместе с занятием, форматом и тем, создана ли по нему задача
/// </summary>
/// <param name="Lesson">Занятие</param>
/// <param name="Format">Формат, выбранный для занятия</param>
/// <param name="Item">Задание</param>
/// <param name="Number">Номер задания в занятии, с единицы</param>
/// <param name="Created">Задача в SingularityApp уже создана</param>
public sealed record HomeworkRow(Lesson Lesson, HomeworkFormatEnum Format, HomeworkItem Item, int Number, bool Created);