namespace Steward.Homework.Models;

/// <summary>
/// Занятие, по которому может быть домашнее задание
/// </summary>
/// <param name="Id">Постоянный ключ занятия: латиница, цифры, дефис и подчёркивание</param>
/// <param name="Title">Название предмета и темы</param>
/// <param name="Date">День занятия</param>
public sealed record Lesson(string Id, string Title, DateOnly Date);