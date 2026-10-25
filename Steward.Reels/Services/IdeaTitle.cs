namespace Steward.Reels.Services;

/// <summary>
/// Подбирает короткое название идеи для списка
/// </summary>
public static class IdeaTitle
{
    /// <summary>
    /// Наибольшая длина названия в символах
    /// </summary>
    private const int MAX_LENGTH = 80;

    /// <summary>
    /// Берёт первую строку подписи, а если подписи нет, первое предложение расшифровки, а если нет и её, имя автора
    /// </summary>
    /// <param name="caption">Подпись под рилсом</param>
    /// <param name="transcript">Расшифровка речи</param>
    /// <param name="author">Автор рилса</param>
    /// <returns>Название не длиннее лимита, оборванное по слову с многоточием</returns>
    public static string Build(string caption, string transcript, string author)
    {
        var line = caption.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        if (line is null)
        {
            var sentence = transcript.Trim().Split(['.', '!', '?', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            line = sentence ?? (author.Length > 0 ? $"Рилс {author}" : "Рилс");
        }

        return Cut(line);
    }

    /// <summary>
    /// Обрывает строку по границе слова
    /// </summary>
    /// <param name="text">Строка</param>
    /// <returns>Та же строка или её начало с многоточием</returns>
    private static string Cut(string text)
    {
        if (text.Length <= MAX_LENGTH)
            return text;

        var cut = text[..MAX_LENGTH];
        var space = cut.LastIndexOf(' ');

        return (space > MAX_LENGTH / 2 ? cut[..space] : cut).TrimEnd() + "…";
    }
}