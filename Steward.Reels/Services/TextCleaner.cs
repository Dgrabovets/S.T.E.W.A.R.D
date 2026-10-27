using System.Globalization;
using System.Text;

namespace Steward.Reels.Services;

/// <summary>
/// Чистит текст идеи от смайликов и скрытых символов, чтобы в названии и описании было только то, что можно прочитать
/// </summary>
public static class TextCleaner
{
    /// <summary>
    /// Убирает смайлики, символы нулевой ширины и управляющие знаки, а подряд идущие пробелы схлопывает. Переносы строк сохраняются
    /// </summary>
    /// <param name="text">Исходный текст</param>
    /// <returns>Текст без лишних символов</returns>
    public static string Clean(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHidden(rune) || IsPictogram(rune))
                continue;

            builder.Append(rune.Value is '\t' or ' ' ? " " : rune.ToString());
        }

        var lines = builder.ToString().Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Select(line => string.Join(' ', line.Split(' ', StringSplitOptions.RemoveEmptyEntries)));

        return string.Join('\n', lines).Trim('\n', ' ');
    }

    /// <summary>
    /// Чистит текст и сводит его в одну строку
    /// </summary>
    /// <param name="text">Исходный текст</param>
    /// <returns>Одна строка без смайликов и скрытых символов</returns>
    public static string CleanLine(string text) => string.Join(' ', Clean(text).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// Скрытый символ: управляющий, форматирующий (нулевой ширины, направление письма), суррогат, частный или выбор начертания смайлика
    /// </summary>
    /// <param name="rune">Символ</param>
    /// <returns>
    /// <see langword="true"/>, если его не видно, а вредит он только копированию и сравнению
    /// </returns>
    private static bool IsHidden(Rune rune)
    {
        if (rune.Value is '\n' or '\t' or '\r')
            return false;

        return rune.Value is (>= 0xFE00 and <= 0xFE0F) or 0x20E3
            || Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned;
    }

    /// <summary>
    /// Смайлик или пиктограмма: блоки эмодзи и значков. Знаки вроде «№», стрелок и валют остаются
    /// </summary>
    /// <param name="rune">Символ</param>
    /// <returns>
    /// <see langword="true"/>, если это картинка из шрифта
    /// </returns>
    private static bool IsPictogram(Rune rune) => rune.Value is (>= 0x1F000 and <= 0x1FAFF) or (>= 0x2600 and <= 0x27BF) or (>= 0x2B00 and <= 0x2BFF) or 0x203C or 0x2049 or 0x2122 or 0x2139 or (>= 0x231A and <= 0x23FF) or (>= 0x25A0 and <= 0x25FF);
}
