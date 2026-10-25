using Steward.Mail.Common;
using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет ссылки в заметке: на задание в LMS и на само письмо
/// </summary>
public class MailLinksTests
{
    /// <summary>
    /// Страница задания важнее прочих ссылок LMS, ссылка берётся и из списка адресов письма, и из его текста
    /// </summary>
    [Fact]
    public void Assignment_prefers_the_assignment_page()
    {
        var fromLinks = Samples.Mail("1", links: ["https://lms.ranepa.ru/my/", "https://lms.ranepa.ru/mod/assign/view.php?id=1323404"]);
        var fromBody = Samples.Mail("2", body: "Отзыв по ссылке: https://lms.ranepa.ru/mod/assign/view.php?id=77 (откройте)");

        Assert.Equal("https://lms.ranepa.ru/mod/assign/view.php?id=1323404", MailLinks.Assignment(fromLinks));
        Assert.Equal("https://lms.ranepa.ru/mod/assign/view.php?id=77", MailLinks.Assignment(fromBody));
    }

    /// <summary>
    /// Без страницы задания берётся любая ссылка на LMS, без ссылок на LMS ничего
    /// </summary>
    [Fact]
    public void Assignment_falls_back_to_any_lms_link_and_then_to_nothing()
    {
        Assert.Equal("https://lms.ranepa.ru/my/", MailLinks.Assignment(Samples.Mail("1", links: ["https://example.com/", "https://lms.ranepa.ru/my/"])));
        Assert.Null(MailLinks.Assignment(Samples.Mail("2", body: "ссылок нет")));
    }

    /// <summary>
    /// В ссылку на письмо подставляется тема без «Re:» и «Fwd:», закодированная для адреса
    /// </summary>
    [Fact]
    public void The_letter_link_carries_the_cleaned_and_escaped_subject()
    {
        var mail = Samples.Mail("1", subject: "Re: RE: Fwd: Вопрос про зачёт");

        Assert.Equal($"https://mail.360.yandex.ru/#search?request={Uri.EscapeDataString("Вопрос про зачёт")}", MailLinks.Letter(mail, new MailSettings()));
    }

    /// <summary>
    /// Если шаблон пуст, ссылка ведёт на саму веб-почту
    /// </summary>
    [Fact]
    public void An_empty_template_gives_the_web_mail_address() =>
        Assert.Equal("https://mail.example.test/", MailLinks.Letter(Samples.Mail("1"), new MailSettings { MessageLinkTemplate = "", WebMailUrl = "https://mail.example.test/" }));
}