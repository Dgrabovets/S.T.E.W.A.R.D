namespace Steward.Models;

/// <summary>
/// Вкладка читалки расшифровки: разделы разбора занятия, между которыми не нужно листать
/// </summary>
public enum ReaderTabEnum
{
    /// <summary>
    /// Что пройдено и что было на занятии
    /// </summary>
    Covered,

    /// <summary>
    /// Темы и формулировки преподавателя
    /// </summary>
    Topics,

    /// <summary>
    /// Принятые решения и задачи с занятия
    /// </summary>
    Actions,

    /// <summary>
    /// Кто сколько говорил
    /// </summary>
    Speakers,
}
