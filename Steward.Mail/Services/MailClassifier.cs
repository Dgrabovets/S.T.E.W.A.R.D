using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Разбор писем по папке и отправителю
/// Отзывы уже разложены правилом Яндекс Почты в папку «Преподаватели»
/// </summary>
public sealed class MailClassifier(MailSettings mail, ClassifierSettings settings) : IMailClassifier
{
    /// <summary>
    /// Относит письмо к категории: из папки преподавателей это отзыв, от системного отправителя это справка, остальное личное письмо
    /// </summary>
    /// <param name="item">Письмо из почтового ящика</param>
    /// <returns>Категория, от которой зависит, будет ли создана задача</returns>
    public MailCategoryEnum Classify(MailItem item)
    {
        if (string.Equals(item.Folder, mail.TeachersFolder, StringComparison.OrdinalIgnoreCase))
            return MailCategoryEnum.TeacherFeedback;

        if (IsSystemSender(item.FromAddress))
            return MailCategoryEnum.Info;

        return MailCategoryEnum.PersonalReply;
    }

    /// <summary>
    /// Проверяет, что письмо отправила система, а не человек: адрес есть в списке игнорируемых или в его имени есть noreply, no-reply или notification
    /// </summary>
    /// <param name="address">Адрес отправителя</param>
    /// <returns>
    /// <see langword="true"/> для системного отправителя
    /// </returns>
    private bool IsSystemSender(string address)
    {
        if (settings.IgnoredSenders.Any(sender => string.Equals(sender, address, StringComparison.OrdinalIgnoreCase)))
            return true;

        var local = address.Split('@')[0];
        return local.Contains("noreply", StringComparison.OrdinalIgnoreCase)
            || local.Contains("no-reply", StringComparison.OrdinalIgnoreCase)
            || local.Contains("notification", StringComparison.OrdinalIgnoreCase);
    }
}
