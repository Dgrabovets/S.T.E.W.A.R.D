namespace Steward.Homework.Models;

/// <summary>
/// Занятие, по которому могут быть домашние задания
/// </summary>
/// <param name="Id">Уникальный ключ занятия: латиница, цифры, дефис и подчёркивание</param>
/// <param name="Title">Название предмета или занятия</param>
/// <param name="Date">Дата занятия</param>
/// <param name="Label">Подпись занятия в расписании, например «Лекция 1». Пусто, если она ничего не добавляет к дате</param>
/// <param name="RecordUrl">Адрес записи занятия. Пусто, если записи нет</param>
/// <param name="Minutes">Длительность записи в минутах. Ноль, если неизвестна</param>
public sealed record Lesson(string Id, string Title, DateOnly Date, string Label = "", string RecordUrl = "", int Minutes = 0);
