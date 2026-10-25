using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Одна проверка почты: читает новые письма, создаёт задачи, запоминает обработанные и показывает уведомление
/// </summary>
/// <param name="reader">Читает непрочитанные письма</param>
/// <param name="classifier">Относит письмо к категории</param>
/// <param name="singularity">Создаёт задачи в SingularityApp</param>
/// <param name="state">Помнит обработанные письма</param>
/// <param name="notifier">Показывает уведомление со сводкой</param>
/// <param name="settings">Настройки приложения</param>
/// <param name="timeProvider">Часы: от них берётся день начала задачи</param>
/// <param name="log">Принимает строки лога</param>
public sealed class MailChecker(
    IMailReader reader,
    IMailClassifier classifier,
    ISingularityClient singularity,
    IStateStore state,
    INotifier notifier,
    AppSettings settings,
    TimeProvider timeProvider,
    Action<string> log)
{
    /// <summary>
    /// Читает непрочитанные письма, создаёт задачи и пишет по строке на письмо и на итог
    /// </summary>
    /// <param name="dryRun">
    /// <see langword="true"/>, если нужно только записать в лог: задачи не создаются, письма не запоминаются
    /// </param>
    /// <param name="cancellationToken">Отменяет проверку</param>
    /// <returns>Письма по категориям и сводка</returns>
    public async Task<MailCheckResult> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var unread = await reader.GetUnreadAsync(cancellationToken);
        var fresh = unread.Where(item => !state.IsProcessed(item.MessageId)).OrderBy(item => item.Date).ToList();
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        var feedback = new List<MailItem>();
        var replies = new List<MailItem>();
        var info = new List<MailItem>();

        foreach (var mail in fresh)
        {
            var category = classifier.Classify(mail);
            var task = MailTaskBuilder.Build(mail, category, settings, today);

            if (task is not null && !dryRun)
                await singularity.CreateTaskAsync(task, cancellationToken);

            (category switch
            {
                MailCategoryEnum.TeacherFeedback => feedback,
                MailCategoryEnum.PersonalReply => replies,
                _ => info
            }).Add(mail);

            log($"{(dryRun ? "[тест] " : string.Empty)}{CategoryLabel(category)}: {mail.FromName} — {mail.Subject}" +
                (task is null ? string.Empty : $" → задача «{task.Title}»"));

            if (!dryRun)
            {
                // Сохраняем после каждого письма: если следующее упадёт, это письмо не станет задачей во второй раз
                state.MarkProcessed(mail.MessageId);
                state.Save();
            }
        }

        var summary = MailSummary.Build(feedback, replies, info);
        log(summary.Replace('\n', ' '));

        if (feedback.Count + replies.Count > 0)
            notifier.Show("Учебная почта", summary);

        return new MailCheckResult(feedback, replies, info, summary);
    }

    /// <summary>
    /// Короткая подпись категории для строки лога
    /// </summary>
    /// <param name="category">Категория письма</param>
    /// <returns>«Отзыв», «Ответить» или «Инфо»</returns>
    private static string CategoryLabel(MailCategoryEnum category) => category switch
    {
        MailCategoryEnum.TeacherFeedback => "Отзыв",
        MailCategoryEnum.PersonalReply => "Ответить",
        _ => "Инфо"
    };
}