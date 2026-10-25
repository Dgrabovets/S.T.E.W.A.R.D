using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;
using Steward.Mail.Tests.Fakes;

namespace Steward.Mail.Tests;

/// <summary>
/// Проверяет проверку почты целиком: какие задачи создаются, что запоминается и что показывается
/// </summary>
public class MailCheckerTests
{
    /// <summary>
    /// Настройки: папка преподавателей по умолчанию, один игнорируемый адрес
    /// </summary>
    private static readonly AppSettings Settings = new() { Classifier = new ClassifierSettings { IgnoredSenders = ["bot@example.com"] } };

    /// <summary>
    /// Отзыв и личное письмо дают задачи, справка нет, и все три письма запоминаются
    /// </summary>
    [Fact]
    public async Task Run_creates_tasks_for_feedback_and_replies_and_remembers_all_mails()
    {
        var singularity = new RecordingSingularity();
        var state = new MemoryState();
        var checker = Checker(
            [
                Samples.Mail("feedback", subject: "Пользователь Петров Пётр дал отзыв на задание «А»", folder: "Преподаватели", hour: 8),
                Samples.Mail("reply", hour: 9),
                Samples.Mail("info", fromAddress: "bot@example.com", hour: 10)
            ],
            singularity,
            state,
            new RecordingNotifier(),
            out _);

        var result = await checker.RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(2, singularity.Created.Count);
        Assert.All(singularity.Created, task => Assert.Equal("2026-10-25", task.Start));
        Assert.Equal(["feedback", "reply", "info"], new[] { "feedback", "reply", "info" }.Where(state.Saved.Contains).ToArray());
        Assert.Single(result.Feedback);
        Assert.Single(result.Replies);
        Assert.Single(result.Info);
    }

    /// <summary>
    /// Уже обработанное письмо пропускается, письма идут от старых к новым
    /// </summary>
    [Fact]
    public async Task Run_skips_processed_mails_and_goes_oldest_first()
    {
        var singularity = new RecordingSingularity();
        var state = new MemoryState();
        state.MarkProcessed("seen");
        var checker = Checker(
            [Samples.Mail("late", subject: "позднее", hour: 12), Samples.Mail("seen", subject: "уже было", hour: 7), Samples.Mail("early", subject: "раннее", hour: 8)],
            singularity,
            state,
            new RecordingNotifier(),
            out _);

        await checker.RunAsync(dryRun: false, CancellationToken.None);

        Assert.Equal(2, singularity.Created.Count);
        Assert.Contains("раннее", singularity.Created[0].Title, StringComparison.Ordinal);
        Assert.Contains("позднее", singularity.Created[1].Title, StringComparison.Ordinal);
    }

    /// <summary>
    /// Пробный прогон только пишет в лог: задач нет, письма не запоминаются
    /// </summary>
    [Fact]
    public async Task A_dry_run_creates_nothing_and_remembers_nothing()
    {
        var singularity = new RecordingSingularity();
        var state = new MemoryState();
        var checker = Checker([Samples.Mail("reply")], singularity, state, new RecordingNotifier(), out var log);

        await checker.RunAsync(dryRun: true, CancellationToken.None);

        Assert.Empty(singularity.Created);
        Assert.Empty(state.Saved);
        Assert.False(state.IsProcessed("reply"));
        Assert.Contains(log, line => line.StartsWith("[тест] Ответить:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Если вторая задача не создалась, первое письмо уже сохранено и не превратится в задачу второй раз
    /// </summary>
    [Fact]
    public async Task A_failure_in_the_middle_keeps_the_mails_that_were_already_done()
    {
        var singularity = new RecordingSingularity { FailOnCall = 2 };
        var state = new MemoryState();
        var checker = Checker([Samples.Mail("first", hour: 8), Samples.Mail("second", hour: 9)], singularity, state, new RecordingNotifier(), out _);

        await Assert.ThrowsAsync<HttpRequestException>(() => checker.RunAsync(dryRun: false, CancellationToken.None));

        Assert.Equal(["first"], state.Saved.ToArray());
    }

    /// <summary>
    /// Уведомление показывается, когда есть отзыв или личное письмо, и молчит, если пришла только справка
    /// </summary>
    [Fact]
    public async Task The_notification_appears_only_for_important_mails()
    {
        var onlyInfo = new RecordingNotifier();
        await Checker([Samples.Mail("info", fromAddress: "bot@example.com")], new RecordingSingularity(), new MemoryState(), onlyInfo, out _)
            .RunAsync(dryRun: false, CancellationToken.None);

        var withReply = new RecordingNotifier();
        await Checker([Samples.Mail("reply")], new RecordingSingularity(), new MemoryState(), withReply, out _)
            .RunAsync(dryRun: false, CancellationToken.None);

        Assert.Empty(onlyInfo.Shown);
        Assert.Equal("Учебная почта", withReply.Shown.Single().Title);
    }

    /// <summary>
    /// Собирает проверку на подделках с фиксированной датой 25 октября 2026 года
    /// </summary>
    /// <param name="mails">Письма ящика</param>
    /// <param name="singularity">Подделка SingularityApp</param>
    /// <param name="state">Список обработанных писем</param>
    /// <param name="notifier">Подделка уведомлений</param>
    /// <param name="log">Строки лога, которые запишет проверка</param>
    /// <returns>Проверка почты</returns>
    private static MailChecker Checker(MailItem[] mails, RecordingSingularity singularity, MemoryState state, RecordingNotifier notifier, out List<string> log)
    {
        var lines = new List<string>();
        log = lines;

        return new MailChecker(
            new FakeMailReader(mails),
            new MailClassifier(Settings.Mail, Settings.Classifier),
            singularity,
            state,
            notifier,
            Settings,
            new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
            lines.Add);
    }
}