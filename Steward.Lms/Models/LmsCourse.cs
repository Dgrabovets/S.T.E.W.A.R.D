namespace Steward.Lms.Models;

/// <summary>
/// Курс из личного кабинета LMS
/// </summary>
/// <param name="Id">Номер курса</param>
/// <param name="Name">Полное название курса с преподавателями и группой</param>
public sealed record LmsCourse(int Id, string Name);
