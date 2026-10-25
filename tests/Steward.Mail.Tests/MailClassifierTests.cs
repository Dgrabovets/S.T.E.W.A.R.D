using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет, как письма делятся на отзывы, личные ответы и справочные
/// </summary>
public class MailClassifierTests
{
    /// <summary>
    /// Классификатор с папкой преподавателей и одним игнорируемым адресом
    /// </summary>
    private readonly MailClassifier _classifier = new(new MailSettings(), new ClassifierSettings { IgnoredSenders = ["bot@example.com"] });

    /// <summary>
    /// Письмо из папки преподавателей всегда отзыв, даже если отправитель выглядит системным
    /// </summary>
    [Fact]
    public void Classify_treats_the_teachers_folder_as_feedback()
    {
        var mail = Samples.Mail("1", folder: "преподаватели", fromAddress: "noreply@lms.example.com");

        Assert.Equal(MailCategoryEnum.TeacherFeedback, _classifier.Classify(mail));
    }

    /// <summary>
    /// Системные адреса и адреса из списка игнорируемых дают справку, а не задачу
    /// </summary>
    [Theory]
    [InlineData("noreply@example.com")]
    [InlineData("No-Reply@example.com")]
    [InlineData("notification@example.com")]
    [InlineData("BOT@example.com")]
    public void Classify_marks_system_senders_as_info(string address)
    {
        var mail = Samples.Mail("1", fromAddress: address);

        Assert.Equal(MailCategoryEnum.Info, _classifier.Classify(mail));
    }

    /// <summary>
    /// Письмо живого человека во «Входящих» требует ответа
    /// </summary>
    [Fact]
    public void Classify_treats_a_person_as_a_personal_reply()
    {
        Assert.Equal(MailCategoryEnum.PersonalReply, _classifier.Classify(Samples.Mail("1")));
    }
}