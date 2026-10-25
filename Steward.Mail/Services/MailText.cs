namespace Steward.Mail.Services;

/// <summary>
/// Короткие тексты из полей письма
/// </summary>
public static class MailText
{
    /// <summary>
    /// Сокращает «Иванов Иван Иванович» до «Иванов И. И.»
    /// </summary>
    /// <param name="fullName">Имя отправителя или адрес</param>
    /// <returns>Фамилия и инициалы. Адрес и имя из одного слова остаются как есть</returns>
    public static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || fullName.Contains('@'))
            return fullName;

        return parts[0] + " " + string.Join(" ", parts.Skip(1).Take(2).Select(part => part[0] + "."));
    }

    /// <summary>
    /// Обрезает строку до заданной длины
    /// </summary>
    /// <param name="value">Исходный текст</param>
    /// <param name="max">Наибольшая длина без многоточия</param>
    /// <returns>Тот же текст или его начало с многоточием</returns>
    public static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";
}