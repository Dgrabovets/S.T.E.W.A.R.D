using System.Text.RegularExpressions;

namespace Steward.Models;

/// <summary>
/// Достаёт из ссылки на Google Таблицу идентификатор таблицы и номер листа
/// </summary>
public static partial class SheetLink
{
    /// <summary>
    /// Идентификатор между <c>/d/</c> и <c>/edit</c>
    /// </summary>
    [GeneratedRegex(@"/spreadsheets/d/([A-Za-z0-9_-]+)", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    /// <summary>
    /// Номер листа в адресе вкладки
    /// </summary>
    [GeneratedRegex(@"[#?&]gid=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex GidPattern();

    /// <summary>
    /// Голый идентификатор без адреса: длинный набор букв, цифр, дефисов и подчёркиваний
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_-]{25,}$", RegexOptions.CultureInvariant)]
    private static partial Regex BareIdPattern();

    /// <summary>
    /// Достаёт идентификатор таблицы
    /// </summary>
    /// <param name="text">Ссылка на таблицу или сам идентификатор</param>
    /// <param name="id">Идентификатор, если он найден</param>
    /// <returns>
    /// <see langword="true"/>, если идентификатор найден
    /// </returns>
    public static bool TryId(string text, out string id)
    {
        var trimmed = text.Trim();
        var match = IdPattern().Match(trimmed);
        id = match.Success ? match.Groups[1].Value : BareIdPattern().IsMatch(trimmed) ? trimmed : string.Empty;

        return id.Length > 0;
    }

    /// <summary>
    /// Достаёт номер листа
    /// </summary>
    /// <param name="text">Ссылка на вкладку таблицы или сам номер</param>
    /// <param name="gid">Номер листа, если он найден</param>
    /// <returns>
    /// <see langword="true"/>, если номер найден
    /// </returns>
    public static bool TryGid(string text, out string gid)
    {
        var trimmed = text.Trim();
        var match = GidPattern().Match(trimmed);
        gid = match.Success ? match.Groups[1].Value : trimmed.Length > 0 && trimmed.All(char.IsAsciiDigit) ? trimmed : string.Empty;

        return gid.Length > 0;
    }
}
