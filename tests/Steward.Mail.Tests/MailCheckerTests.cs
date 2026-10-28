using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;
using Steward.Mail.Tests.Fakes;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет проверку почты целиком: какие задачи создаются, что считается уже обработанным и что показывается
/// </summary>
public class MailCheckerTests
{
    /// <summary>
    /// Настройки: папка преподавателей по умолчанию, один игнорируемый адрес, письма за три дня
    /// </summary>
    private static readonly AppSettings Settings = new() { Classifier = new ClassifierSettings { IgnoredSenders = ["bot@example.com"] } };

    /// <summary>
    /// Отзыв и личное письмо дают задачи с внешним ключом письма, справка задачи не получает
    /// </summary>
    [Fact]
    public async Task Run_creates_tasks_for_feedback_and_replies_but_not_for_info()
    {
        var singularity = new RecordingSingularity();
        var checker = Checker(
            [
                Samples.Mail("feedback", subject: "Пользователь Петров Пётр дал отзыв на задание «А»", folder: "Преподаватели", hour: 8),
                Samples.Mail("reply", hour: 9),
                Samples.Mail("info", fromAddress: "bot@example.com", hour: 10)
            ],
            singularity,
            new RecordingNotifier(),
            out _);

        var result = await checker.RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(["mail:feedback", "mail:reply"], singularity.Created.Select(task => task.ExternalId!).ToArray());
        Assert.All(singularity.Created, task => Assert.Equal("2026-10-25T00:00:00+00:00", task.Start));
        Assert.Single(result.Feedback);
        Assert.Single(result.Replies);
        Assert.Single(result.Info);
    }

    /// <summary>
    /// Письмо, для которого в SingularityApp уже есть задача, пропускается, остальные идут от старых к новым
    /// </summary>
    [Fact]
    public async Task Run_skips_mails_that_already_have_a_task_and_goes_oldest_first()
    {
        var singularity = new RecordingSingularity();
        singularity.Existing.Add("mail:seen");
        var checker = Checker(
            [Samples.Mail("late", subject: "позднее", hour: 12), Samples.Mail("seen", subject: "уже было", hour: 7), Samples.Mail("early", subject: "раннее", hour: 8)],
            singularity,
            new RecordingNotifier(),
            out _);

        await checker.RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(2, singularity.Created.Count);
        Assert.Contains("раннее", singularity.Created[0].Title, StringComparison.Ordinal);
        Assert.Contains("позднее", singularity.Created[1].Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// Все разобранные письма помечаются прочитанными, а пробный запуск ящик не трогает
    /// </summary>
    [Fact]
    public async Task Processed_mails_are_marked_read_but_a_dry_run_leaves_the_mailbox_alone()
    {
        var mails = new[] { Samples.Mail("reply", hour: 9), Samples.Mail("info", fromAddress: "bot@example.com", hour: 10) };
        var trialBox = new FakeMailReader(mails);
        var box = new FakeMailReader(mails);

        await Checker(mails, new RecordingSingularity(), new RecordingNotifier(), out _, reader: trialBox).RunAsync(dryRun: true, CancellationToken.None);
        await Checker(mails, new RecordingSingularity(), new RecordingNotifier(), out _, reader: box).RunAsync(dryRun: false, CancellationToken.None);

        Assert.Empty(trialBox.Marked);
        Assert.Equal(["reply", "info"], box.Marked.Select(mail => mail.MessageId).ToArray());
    }

    /// <summary>
    /// Информационное письмо без задачи разбирается один раз: повторная проверка его уже не показывает, а пробный запуск его не запоминает
    /// </summary>
    [Fact]
    public async Task An_info_mail_is_reported_once_and_a_dry_run_does_not_remember_it()
    {
        var handled = new FakeHandledMail();
        var mails = new[] { Samples.Mail("info", fromAddress: "bot@example.com", hour: 10) };

        var trial = await Checker(mails, new RecordingSingularity(), new RecordingNotifier(), out _, handled).RunAsync(dryRun: true, CancellationToken.None);
        var first = await Checker(mails, new RecordingSingularity(), new RecordingNotifier(), out _, handled).RunAsync(dryRun: false, CancellationToken.None);
        var second = await Checker(mails, new RecordingSingularity(), new RecordingNotifier(), out _, handled).RunAsync(dryRun: false, CancellationToken.None);

        Assert.Single(trial.Info);
        Assert.Single(first.Info);
        Assert.Empty(second.Info);
        Assert.Equal(["info"], handled.Ids.ToArray());
    }

    /// <summary>
    /// Письмо остаётся непрочитанным, и повторная проверка не создаёт дублей
    /// </summary>
    [Fact]
    public async Task A_second_run_over_the_same_unread_mails_creates_no_duplicates()
    {
        var singularity = new RecordingSingularity();
        var checker = Checker([Samples.Mail("reply", hour: 9), Samples.Mail("other", hour: 10)], singularity, new RecordingNotifier(), out _);

        await checker.RunAsync(dryRun: false, CancellationToken.None);
        await checker.RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(2, singularity.Created.Count);
    }

    /// <summary>
    /// Пробный прогон только пишет в лог: задач нет
    /// </summary>
    [Fact]
    public async Task A_dry_run_creates_nothing()
    {
        var singularity = new RecordingSingularity();
        var checker = Checker([Samples.Mail("reply")], singularity, new RecordingNotifier(), out var log);

        await checker.RunAsync(dryRun: true, CancellationToken.None);

        Assert.Empty(singularity.Created);
        Assert.Contains(log, line => line.StartsWith("[тест] Ответить:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Строка про письмо в логе заканчивается ссылкой на письмо и коротким описанием, их читает окно приложения
    /// </summary>
    [Fact]
    public async Task A_log_line_carries_the_letter_link_and_a_short_description()
    {
        var checker = Checker([Samples.Mail("reply")], new RecordingSingularity(), new RecordingNotifier(), out var log);

        await checker.RunAsync(dryRun: true, CancellationToken.None);

        var line = Assert.Single(log, entry => entry.StartsWith("[тест] Ответить:", StringComparison.Ordinal));
        var parts = line.Split(" ‖ ");
        Assert.Equal(3, parts.Length);
        Assert.StartsWith("http", parts[1], StringComparison.Ordinal);
        Assert.True(parts[2].Length > 0 && parts[2].Length <= 221);
    }

    /// <summary>
    /// Если вторая задача не создалась, первое письмо уже имеет задачу, и следующая проверка создаёт только вторую
    /// </summary>
    [Fact]
    public async Task A_failure_in_the_middle_does_not_repeat_the_task_that_was_created()
    {
        var failing = new RecordingSingularity { FailOnCall = 2 };
        var mails = new[] { Samples.Mail("first", hour: 8), Samples.Mail("second", hour: 9) };

        await Assert.ThrowsAsync<HttpRequestException>(() => Checker(mails, failing, new RecordingNotifier(), out _).RunAsync(dryRun: false, CancellationToken.None));

        var retry = new RecordingSingularity();
        retry.Existing.UnionWith(failing.Existing);
        await Checker(mails, retry, new RecordingNotifier(), out _).RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(["mail:first"], failing.Created.Select(task => task.ExternalId!).ToArray());
        Assert.Equal(["mail:second"], retry.Created.Select(task => task.ExternalId!).ToArray());
    }

    /// <summary>
    /// Задачи ищутся от начала дня минус дни просмотра и ещё один день, а без непрочитанных писем SingularityApp не спрашивается вовсе
    /// </summary>
    [Fact]
    public async Task The_search_window_covers_the_lookback_days_and_is_skipped_without_mail()
    {
        var withMail = new RecordingSingularity();
        await Checker([Samples.Mail("reply")], withMail, new RecordingNotifier(), out _).RunAsync(dryRun: false, CancellationToken.None);

        var withoutMail = new RecordingSingularity();
        await Checker([], withoutMail, new RecordingNotifier(), out _).RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(new DateTimeOffset(2026, 10, 21, 0, 0, 0, TimeSpan.Zero), withMail.LastStartsAfter);
        Assert.Null(withoutMail.LastStartsAfter);
    }

    /// <summary>
    /// Уведомление показывается, когда есть отзыв или личное письмо, и молчит, если пришла только справка
    /// </summary>
    [Fact]
    public async Task The_notification_appears_only_for_important_mails()
    {
        var onlyInfo = new RecordingNotifier();
        await Checker([Samples.Mail("info", fromAddress: "bot@example.com")], new RecordingSingularity(), onlyInfo, out _)
            .RunAsync(dryRun: false, CancellationToken.None);

        var withReply = new RecordingNotifier();
        await Checker([Samples.Mail("reply")], new RecordingSingularity(), withReply, out _)
            .RunAsync(dryRun: false, CancellationToken.None);

        Assert.Empty(onlyInfo.Shown);
        Assert.Equal("Учебная почта", withReply.Shown.Single().Title);
    }

    /// <summary>
    /// Собирает проверку на подделках с фиксированной датой 25 октября 2026 года
    /// </summary>
    /// <param name="mails">Письма ящика</param>
    /// <param name="singularity">Подделка SingularityApp</param>
    /// <param name="notifier">Подделка уведомлений</param>
    /// <param name="log">Строки лога, которые запишет проверка</param>
    /// <param name="handled">Память о разобранных письмах. Без неё каждый вызов начинает с пустой</param>
    /// <param name="reader">Почтовый ящик. Без него берётся ящик с переданными письмами</param>
    /// <returns>Проверка почты</returns>
    private static MailChecker Checker(MailItem[] mails, RecordingSingularity singularity, RecordingNotifier notifier, out List<string> log, FakeHandledMail? handled = null, FakeMailReader? reader = null)
    {
        var lines = new List<string>();
        log = lines;

        return new MailChecker(
            reader ?? new FakeMailReader(mails),
            new MailClassifier(Settings.Mail, Settings.Classifier),
            singularity,
            handled ?? new FakeHandledMail(),
            notifier,
            Settings,
            new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
            lines.Add);
    }
}