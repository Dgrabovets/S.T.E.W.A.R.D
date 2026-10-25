using System.Net;
using System.Text;
using MimeKit.Text;

namespace Steward.Mail.Services;

/// <summary>
/// Превращает HTML-письмо в простой текст
/// </summary>
public static class MailHtml
{
    /// <summary>
    /// Теги, после которых начинается новая строка
    /// </summary>
    private static readonly HashSet<HtmlTagId> LineBreaks =
    [
        HtmlTagId.Br, HtmlTagId.P, HtmlTagId.Div, HtmlTagId.LI, HtmlTagId.TR, HtmlTagId.H1, HtmlTagId.H2, HtmlTagId.H3, HtmlTagId.BlockQuote,
    ];

    /// <summary>
    /// Убирает теги, стили и скрипты, переводит блоки в строки
    /// </summary>
    /// <param name="html">Текст HTML-части письма</param>
    /// <returns>Текст без разметки</returns>
    public static string ToText(string html)
    {
        var builder = new StringBuilder();
        var skipped = false;
        var tokenizer = new HtmlTokenizer(new StringReader(html));

        while (tokenizer.ReadNextToken(out var token))
        {
            if (token is HtmlTagToken tag)
            {
                if (tag.Id is HtmlTagId.Script or HtmlTagId.Style)
                    skipped = !tag.IsEndTag;

                if (LineBreaks.Contains(tag.Id))
                    builder.Append('\n');
            }
            else if (token is HtmlDataToken data && !skipped)
            {
                builder.Append(WebUtility.HtmlDecode(data.Data));
            }
        }

        return builder.ToString();
    }
}