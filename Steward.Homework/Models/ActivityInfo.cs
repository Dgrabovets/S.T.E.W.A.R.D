namespace Steward.Homework.Models;

/// <summary>
/// Работа из LMS в том виде, который нужен для счётчиков на плитке предмета: задание или тест и сдано ли оно
/// </summary>
/// <param name="Subject">Название предмета</param>
/// <param name="Name">Название работы</param>
/// <param name="IsQuiz">Это тест, а не задание</param>
/// <param name="Done">Работа сдана</param>
public sealed record ActivityInfo(string Subject, string Name, bool IsQuiz, bool Done);
