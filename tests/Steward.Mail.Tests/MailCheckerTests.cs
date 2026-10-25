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
    /// <returns>Проверка почты</returns>
    private static MailChecker Checker(MailItem[] mails, RecordingSingularity singularity, RecordingNotifier notifier, out List<string> log)
    {
        var lines = new List<string>();
        log = lines;

        return new MailChecker(
            new FakeMailReader(mails),
            new MailClassifier(Settings.Mail, Settings.Classifier),
            singularity,
            notifier,
            Settings,
            new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
            lines.Add);
    }
}