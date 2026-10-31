using Steward.Homework.Models;

namespace Steward.ViewModels;

/// <summary>
/// Вариант в списке «Откуда задания»: значение, название и пояснение
/// </summary>
/// <param name="Source">Источник заданий</param>
/// <param name="Title">Название в списке</param>
/// <param name="Hint">Что это значит, одной фразой</param>
public sealed record TaskSourceOptionViewModel(TaskSourceEnum Source, string Title, string Hint);
