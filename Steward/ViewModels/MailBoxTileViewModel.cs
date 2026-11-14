using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Плитка одного почтового ящика на странице «Почта»: сколько писем ждёт ответа, сколько отзывов и когда была последняя проверка
/// </summary>
/// <param name="Name">Название ящика</param>
/// <param name="NeedReply">Сколько разобранных писем требуют ответа</param>
/// <param name="Feedback">Сколько писем с отзывами</param>
/// <param name="LastCheck">Когда была последняя проверка ящика</param>
/// <param name="Tone">Цвет плитки: внимание, если есть письма без ответа, проблема, если проверка не прошла</param>
public sealed record MailBoxTileViewModel(string Name, int NeedReply, int Feedback, string LastCheck, PanelToneEnum Tone)
{
    /// <summary>
    /// Главная строка плитки: что требует действия
    /// </summary>
    public string Headline => NeedReply > 0 ? $"Ответить: {NeedReply}" : "Ответов не требуется";

    /// <summary>
    /// Вторая строка плитки: отзывы и последняя проверка
    /// </summary>
    public string Detail => string.Join(" · ", new[] { Feedback > 0 ? $"отзывов: {Feedback}" : string.Empty, "проверка " + LastCheck }.Where(part => part.Length > 0));
}
