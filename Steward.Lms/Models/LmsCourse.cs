namespace Steward.Lms.Models;

/// <summary>
/// Курс из личного кабинета LMS
/// </summary>
/// <param name="Id">Номер курса</param>
/// <param name="Name">Полное название курса с преподавателями и группой</param>
public sealed record LmsCourse(int Id, string Name)
{
    /// <summary>
    /// Название предмета: полное название до первой квадратной скобки, без семестра, преподавателей и группы
    /// </summary>
    public string Subject
    {
        get
        {
            var bracket = Name.IndexOf(" [", StringComparison.Ordinal);

            return (bracket > 0 ? Name[..bracket] : Name).Trim();
        }
    }
}
