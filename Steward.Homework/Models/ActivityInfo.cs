namespace Steward.Homework.Models;

/// <summary>
/// Работа из LMS в том виде, который нужен для счётчиков на плитке предмета: задание или тест и сдано ли оно
/// </summary>
/// <param name="Subject">Название предмета</param>
/// <param name="Name">Название работы</param>
/// <param name="IsQuiz">Это тест, а не задание</param>
/// <param name="Done">Работа сдана</param>
/// <param name="Section">Раздел курса, в котором лежит работа</param>
/// <param name="Due">Срок сдачи или <see langword="null"/>, если его нет</param>
/// <param name="Id">Номер работы в LMS: по нему лежит папка с описанием и файлами задания</param>
public sealed record ActivityInfo(string Subject, string Name, bool IsQuiz, bool Done, string Section = "", DateOnly? Due = null, string Id = "");
