namespace Steward.ViewModels;

/// <summary>
/// Место в расшифровке, где сказано задание: название задания, цитата, строки вокруг неё и пояснение, если цитату показать нельзя
/// </summary>
/// <param name="Title">Название задания</param>
/// <param name="Quote">Цитата в кавычках или пустая строка, если её нет</param>
/// <param name="Lines">Строки расшифровки вокруг цитаты, строки с цитатой отмечены</param>
/// <param name="Note">Пояснение, почему цитаты нет. Пусто, если строки показаны</param>
public sealed record QuoteViewModel(string Title, string Quote, IReadOnlyList<ExcerptLineViewModel> Lines, string Note)
{
    /// <summary>
    /// Есть строки расшифровки для показа
    /// </summary>
    public bool HasLines => Lines.Count > 0;

    /// <summary>
    /// Есть пояснение
    /// </summary>
    public bool HasNote => Note.Length > 0;
}

/// <summary>
/// Одна строка отрывка расшифровки
/// </summary>
/// <param name="Text">Строка как в расшифровке</param>
/// <param name="IsHit">В строке сказана цитата: строка выделяется</param>
public sealed record ExcerptLineViewModel(string Text, bool IsHit);
