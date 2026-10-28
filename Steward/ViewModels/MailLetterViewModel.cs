using System.Globalization;
using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Разобранное письмо на странице «Почта»: тип, тема, отправитель и что с ним сделано
/// </summary>
/// <param name="Kind">Тип: «Ответить», «Отзыв» или «Инфо»</param>
/// <param name="Subject">Тема или суть письма</param>
/// <param name="Sender">От кого. Пусто, если из вывода это не видно</param>
/// <param name="Meta">Строка под темой: отправитель, время и что сделано</param>
/// <param name="Tone">Цвет карточки по типу письма</param>
public sealed record MailLetterViewModel(string Kind, string Subject, string Sender, string Meta, PanelToneEnum Tone)
{
    /// <summary>
    /// Собирает карточку из строки вывода почты вида «Ответить: Иванов Иван — Вопрос → задача «…»»
    /// </summary>
    /// <param name="line">Строка вывода со временем проверки</param>
    /// <returns>Карточка письма</returns>
    public static MailLetterViewModel From(LogLine line)
    {
        var text = line.Text.Replace("[тест] ", string.Empty, StringComparison.Ordinal);
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        var kind = colon > 0 ? text[..colon].Trim() : "Инфо";
        var rest = colon > 0 ? text[(colon + 1)..].Trim() : text;

        var created = false;
        var arrow = rest.IndexOf('→', StringComparison.Ordinal);
        if (arrow > 0)
        {
            created = true;
            rest = rest[..arrow].Trim();
        }

        var sender = string.Empty;
        var dash = rest.IndexOf(" — ", StringComparison.Ordinal);
        if (dash > 0)
        {
            sender = rest[..dash].Trim();
            rest = rest[(dash + 3)..].Trim();
        }

        var when = DateTime.TryParse(line.Time, new CultureInfo("ru-RU"), DateTimeStyles.None, out var parsed) ? parsed.ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) : line.Time;
        var meta = string.Join(" · ", new[] { sender, when, created ? "задача создана в Singularity" : string.Empty }.Where(part => part.Length > 0));

        return new MailLetterViewModel(kind, rest, sender, meta, line.Tone);
    }
}
