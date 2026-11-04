using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Одна проверка почты: читает непрочитанные письма, создаёт задачи для тех, у кого её ещё нет, помечает разобранные прочитанными и показывает уведомление. Разобранные письма помечаются прочитанными в ящике. Повторную задачу не даёт внешний ключ в SingularityApp, а повторный разбор письма без задачи не даёт память о разобранных
/// </summary>
/// <param name="reader">Читает непрочитанные письма</param>
/// <param name="classifier">Относит письмо к категории</param>
/// <param name="singularity">Создаёт задачи в SingularityApp и подсказывает, для каких писем они уже есть</param>
/// <param name="handled">Помнит письма, которые уже разобраны, в том числе информационные без задач</param>
/// <param name="notifier">Показывает уведомление со сводкой</param>
/// <param name="settings">Настройки приложения</param>
/// <param name="timeProvider">Часы: от них берётся день начала задачи</param>
/// <param name="log">Принимает строки лога</param>
/// <param name="archive">Сохраняет важные письма (от человека и с отзывом) с заголовком, отправителем и началом текста. Пусто: архива нет</param>
public sealed class MailChecker(
    IMailReader reader,
    IMailClassifier classifier,
    ISingularityClient singularity,
    IHandledMail handled,
    INotifier notifier,
    AppSettings settings,
    TimeProvider timeProvider,
    Action<string> log,
    IMailArchive? archive = null)
{
    /// <summary>
    /// Длина описания письма в логе в знаках
    /// </summary>
    private const int DESCRIPTION_LENGTH = 220;

    /// <summary>
    /// Длина начала письма в архиве в знаках
    /// </summary>
    private const int PREVIEW_LENGTH = 500;

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

        var fresh = unread.Where(item => !existing.Contains(MailTaskBuilder.ExternalId(item)) && !handled.Contains(item.MessageId)).OrderBy(item => item.Date).ToList();

        var feedback = new List<MailItem>();
        var replies = new List<MailItem>();
        var info = new List<MailItem>();
        var done = new List<MailItem>();

        foreach (var mail in fresh)
        {
            var category = classifier.Classify(mail);
            var task = MailTaskBuilder.Build(mail, category, settings, dayStart);

            // Архив пишется раньше задачи: если он не записался, письмо остаётся нетронутым и придёт снова, а повторная запись в архив его не задвоит. После создания задачи письмо уже не вернулось бы, потому что внешний ключ задачи пропускает его
            if (!dryRun && category is MailCategoryEnum.TeacherFeedback or MailCategoryEnum.PersonalReply && !SaveToArchive(mail, category, task is not null, now))
                continue;

            if (task is not null && !dryRun)
                await singularity.CreateTaskAsync(task, cancellationToken);

            if (!dryRun)
            {
                handled.Add(mail.MessageId);
                done.Add(mail);
            }

            (category switch
            {
                MailCategoryEnum.TeacherFeedback => feedback,
                MailCategoryEnum.PersonalReply => replies,
                _ => info
            }).Add(mail);

            log($"{(dryRun ? "[тест] " : string.Empty)}{CategoryLabel(category)}: {mail.FromName} — {mail.Subject}" +
                (task is null ? string.Empty : $" → задача «{task.Title}»") + Details(mail));

        }

        if (!dryRun && done.Count > 0)
            await MarkSeenAsync(done, cancellationToken);

        var summary = MailSummary.Build(feedback, replies, info);
        log(summary.Replace('\n', ' '));

        if (feedback.Count + replies.Count > 0)
            notifier.Show("Учебная почта", summary);

        return new MailCheckResult(feedback, replies, info, summary);
    }

    /// <summary>
    /// Кладёт важное письмо в архив. Сбой архива не роняет проверку, а только оставляет письмо неразобранным
    /// </summary>
    /// <param name="mail">Письмо</param>
    /// <param name="category">Категория письма</param>
    /// <param name="taskCreated">По письму заведена задача</param>
    /// <param name="now">Время разбора</param>
    /// <returns><see langword="true"/>, если письмо в архиве или архива нет; <see langword="false"/>, если записать не вышло</returns>
    private bool SaveToArchive(MailItem mail, MailCategoryEnum category, bool taskCreated, DateTimeOffset now)
    {
        try
        {
            archive?.Add(new ArchivedLetter(
                mail.MessageId,
                CategoryLabel(category),
                mail.FromName,
                mail.FromAddress,
                mail.Subject,
                mail.Date,
                now,
                MailText.Truncate(string.Join(' ', mail.Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), PREVIEW_LENGTH),
                MailLinks.Letter(mail, settings.Mail),
                taskCreated));

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log($"Не удалось сохранить письмо в архив, оно будет разобрано при следующей проверке: {exception.Message}");

            return false;
        }
    }

    /// <summary>
    /// Хвост строки лога для окна приложения: ссылка на письмо и короткое описание, разделённые знаком «‖»
    /// </summary>
    /// <param name="mail">Письмо</param>
    /// <returns>Строка вида « ‖ ссылка ‖ описание»</returns>
    private string Details(MailItem mail) => $" ‖ {MailLinks.Letter(mail, settings.Mail)} ‖ {MailText.Truncate(string.Join(' ', mail.Body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)), DESCRIPTION_LENGTH)}";

    /// <summary>
    /// Помечает разобранные письма прочитанными. Сбой отметки не роняет проверку: задачи уже созданы, а письма записаны в память о разобранных и повторно не придут
    /// </summary>
    /// <param name="mails">Разобранные письма</param>
    /// <param name="cancellationToken">Отменяет проверку</param>
    /// <returns>Задача, которая завершается, когда отметки поставлены или не удались</returns>
    private async Task MarkSeenAsync(IReadOnlyList<MailItem> mails, CancellationToken cancellationToken)
    {
        try
        {
            await reader.MarkSeenAsync(mails, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log($"Не удалось пометить письма прочитанными: {exception.Message}");
        }
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