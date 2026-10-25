using MailToSingularity.Models;

namespace MailToSingularity.Interfaces;

/// <summary>
/// Определение категории письма
/// </summary>
public interface IMailClassifier
{
    /// <summary>
    /// Относит письмо к одной из категорий по папке и отправителю
    /// </summary>
    /// <param name="mail">
    /// Письмо из почтового ящика
    /// </param>
    /// <returns>
    /// Категория, от которой зависит, будет ли создана задача
    /// </returns>
    MailCategory Classify(MailItem mail);
}
