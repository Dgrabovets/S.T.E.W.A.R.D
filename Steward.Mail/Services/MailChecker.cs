using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Одна проверка почты: читает непрочитанные письма, создаёт задачи для тех, у кого её ещё нет, и показывает уведомление. Флаг «прочитано» не ставится: пока вы не открыли письмо, оно считается требующим внимания, а повторную задачу не даёт внешний ключ в SingularityApp
/// </summary>
/// <param name="reader">Читает непрочитанные письма</param>
/// <param name="classifier">Относит письмо к категории</param>
/// <param name="singularity">Создаёт задачи в SingularityApp и подсказывает, для каких писем они уже есть</param>
/// <param name="notifier">Показывает уведомление со сводкой</param>
/// <param name="settings">Настройки приложения</param>
/// <param name="timeProvider">Часы: от них берётся день начала задачи</param>
/// <param name="log">Принимает строки лога</param>
public sealed class MailChecker(
    IMailReader reader,
    IMailClassifier classifier,
    ISingularityClient singularity,
    INotifier notifier,
    AppSettings settings,
    TimeProvider timeProvider,
    Action<string> log)
{
    /// <summary>
    /// Читает непрочитанные письма, создаёт задачи и пишет по строке на письмо и на итог
    /// </summary>
    /// <param name="dryRun">
    /// <see langword="true"/>, если нужно только записать в лог: задачи не создаются
    /// </param>
    /// <param name="cancellationToken">Отменяет проверку</param>
    /// <returns>Письма по категориям и сводка</returns>
    public async Task<MailCheckResult> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetLocalNow();
        var dayStart = new DateTimeOffset(now.Date, now.Offset);

        var unread = await reader.GetUnreadAsync(cancellationToken);
        var existing = unread.Count == 0
            ? new HashSet<string>()
            : await singularity.ListExternalIdsAsync(dayStart.AddDays(-(settings.Mail.LookbackDays + 1)), cancellationToken);

        var fresh = unread.Where(item => !existing.Contains(MailTaskBuilder.ExternalId(item))).OrderBy(item => item.Date).ToList();

        var feedback = new List<MailItem>();
        var replies = new List<MailItem>();
        var info = new List<MailItem>();

        foreach (var mail in fresh)
        {
            var category = classifier.Classify(mail);
            var task = MailTaskBuilder.Build(mail, category, settings, dayStart);

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