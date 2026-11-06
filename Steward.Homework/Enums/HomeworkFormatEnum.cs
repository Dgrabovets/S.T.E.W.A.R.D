namespace Steward.Homework.Models;

/// <summary>
/// В каком виде готовить домашнее задание
/// </summary>
public enum HomeworkFormatEnum
{
    /// <summary>
    /// Документ Word по методическим указаниям
    /// </summary>
    Word,

    /// <summary>
    /// Презентация
    /// </summary>
    Presentation,

    /// <summary>
    /// Текстовый ответ, который вставляется в форму сдачи
    /// </summary>
    Text,

    /// <summary>
    /// Программа или таблица с кодом и пояснением
    /// </summary>
    Code,

    /// <summary>
    /// Не готовить: задание сделаю сам
    /// </summary>
    Skip,

    /// <summary>
    /// Проект из нескольких файлов с кодом и описанием, упакованный в zip: домашние по C# и Unity
    /// </summary>
    Project,
}