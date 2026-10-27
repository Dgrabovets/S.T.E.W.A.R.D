namespace Steward.ViewModels;

/// <summary>
/// Один абзац разбора идеи
/// </summary>
/// <param name="Label">Метка абзаца: «Суть», «Для чего», «Плюсы». Пустая у текста без метки</param>
/// <param name="Text">Текст абзаца</param>
public sealed record IdeaSection(string Label, string Text);
