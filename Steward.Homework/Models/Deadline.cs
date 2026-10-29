namespace Steward.Homework.Models;

/// <summary>
/// Срок сдачи несданной работы: для напоминаний на экране «Статус»
/// </summary>
/// <param name="Subject">Предмет</param>
/// <param name="Title">Что нужно сдать</param>
/// <param name="Due">Дата сдачи</param>
/// <param name="FromLms">Срок взят из LMS, а не из расшифровки занятия</param>
public sealed record Deadline(string Subject, string Title, DateOnly Due, bool FromLms);
