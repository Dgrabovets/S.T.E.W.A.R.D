using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет перевод HTML-письма в текст
/// </summary>
public class MailHtmlTests
{
    /// <summary>
    /// Теги убираются, блоки становятся строками, стили и скрипты и сущности обрабатываются
    /// </summary>
    [Fact]
    public void Tags_are_removed_and_blocks_become_lines()
    {
        var text = MailHtml.ToText("<style>p{color:red}</style><div>Привет,&nbsp;мир</div><p>Вторая <b>строка</b></p><script>alert(1)</script><br>Третья &amp; последняя");

        Assert.DoesNotContain("color", text, StringComparison.Ordinal);
        Assert.DoesNotContain("alert", text, StringComparison.Ordinal);
        Assert.Equal(["Привет, мир", "Вторая строка", "Третья & последняя"], text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => line.Replace('\u00a0', ' ')).ToArray());
    }
}