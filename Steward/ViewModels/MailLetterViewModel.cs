using System.Globalization;
using Steward.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Разобранное письмо на странице «Почта»: тип, тема, отправитель, описание и кнопка открытия письма
/// </summary>
/// <param name="Kind">Тип: «Ответить», «Отзыв» или «Инфо»</param>
/// <param name="Subject">Тема или суть письма</param>
/// <param name="Sender">От кого. Пусто, если из вывода это не видно</param>
/// <param name="Meta">Строка под темой: отправитель, время и что сделано</param>
/// <param name="Tone">Цвет карточки по типу письма</param>
/// <param name="Description">Начало письма. Пусто, если программа почты его не записала</param>
/// <param name="Link">Ссылка на письмо в веб-почте. Пусто, если её нет</param>
/// <param name="OpenCommand">Открывает письмо в браузере</param>
public sealed record MailLetterViewModel(string Kind, string Subject, string Sender, string Meta, PanelToneEnum Tone, string Description, string Link, RelayCommand OpenCommand)
{
    /// <summary>
    /// У письма есть описание
    /// </summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>
    /// У письма есть ссылка
    /// </summary>
    public bool HasLink => Link.Length > 0;

    /// <summary>
    /// Собирает карточку из строки вывода почты вида «Ответить: Иванов Иван — Вопрос → задача «…» ‖ ссылка ‖ описание»
    /// </summary>
    /// <param name="line">Строка вывода со временем проверки</param>
    /// <param name="open">Действие кнопки «Открыть письмо»: принимает ссылку</param>
    /// <returns>Карточка письма</returns>
    public static MailLetterViewModel From(LogLine line, Action<string>? open = null)
    {
        var pieces = line.Text.Replace("[тест] ", string.Empty, StringComparison.Ordinal).Split(" ‖ ", 3, StringSplitOptions.None);
        var text = pieces[0];
        var link = pieces.Length > 1 && Uri.TryCreate(pieces[1].Trim(), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? pieces[1].Trim() : string.Empty;
        var description = pieces.Length > 2 ? pieces[2].Trim() : string.Empty;

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

        return new MailLetterViewModel(kind, rest, sender, meta, line.Tone, description, link, new RelayCommand(() => open?.Invoke(link), () => link.Length > 0));
    }
}
