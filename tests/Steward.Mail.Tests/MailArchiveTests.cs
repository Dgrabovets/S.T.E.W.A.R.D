using Steward.Mail.Common;
using Steward.Mail.Models;
using Steward.Mail.Services;
using Steward.Mail.Tests.Fakes;

namespace Steward.Mail.Tests;

/// <summary>
/// Архив важных писем: хранит заголовок, отправителя и начало текста, не пишет письмо дважды и не берёт справки и пробные запуски
/// </summary>
public class MailArchiveTests
{
    /// <summary>
    /// Настройки: один игнорируемый адрес
    /// </summary>
    private static readonly AppSettings Settings = new() { Classifier = new ClassifierSettings { IgnoredSenders = ["bot@example.com"] } };

    /// <summary>
    /// Собирает проверку с архивом на подделках
    /// </summary>
    /// <param name="mails">Письма ящика</param>
    /// <param name="archive">Архив</param>
    /// <returns>Проверка почты</returns>
    private static MailChecker Checker(MailItem[] mails, MailArchive archive) => new(
        new FakeMailReader(mails),
        new MailClassifier(Settings.Mail, Settings.Classifier),
        new RecordingSingularity(),
        new FakeHandledMail(),
        new RecordingNotifier(),
        Settings,
        new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
        _ => { },
        archive);

    /// <summary>
    /// Письма с отзывом и от человека попадают в архив с темой, отправителем, началом текста и ссылкой; справка не попадает
    /// </summary>
    [Fact]
    public async Task Important_letters_are_archived_with_subject_sender_and_preview_but_info_is_not()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var archive = new MailArchive(Path.Combine(directory.FullName, "mail", "archive.json"));

            await Checker(
                [Samples.Mail("reply", hour: 9), Samples.Mail("info", fromAddress: "bot@example.com", hour: 10)],
                archive).RunAsync(dryRun: false, CancellationToken.None);

            var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(directory.FullName, "mail", "archive.json"))).RootElement;
            Assert.Equal(1, saved.GetArrayLength());
            var letter = saved[0];
            Assert.Equal("Ответить", letter.GetProperty("kind").GetString());
            Assert.Equal("reply", letter.GetProperty("messageId").GetString());
            Assert.True(letter.GetProperty("taskCreated").GetBoolean());
            Assert.NotEqual(string.Empty, letter.GetProperty("subject").GetString());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Одно письмо не пишется дважды, новые идут в начало, а пробный запуск архив не трогает
    /// </summary>
    [Fact]
    public async Task The_archive_skips_duplicates_keeps_newest_first_and_ignores_dry_runs()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(directory.FullName, "archive.json");
            var archive = new MailArchive(path);
            var now = DateTimeOffset.UtcNow;

            archive.Add(new ArchivedLetter("a", "Ответить", "Иванов", "i@x.ru", "Первое", now, now, "текст", string.Empty, true));
            archive.Add(new ArchivedLetter("b", "Отзыв", "Петров", "p@x.ru", "Второе", now, now, "текст", string.Empty, false));
            archive.Add(new ArchivedLetter("a", "Ответить", "Иванов", "i@x.ru", "Первое", now, now, "текст", string.Empty, true));
            await Checker([Samples.Mail("reply", hour: 9)], archive).RunAsync(dryRun: true, CancellationToken.None);

            var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
            Assert.Equal(["b", "a"], saved.EnumerateArray().Select(item => item.GetProperty("messageId").GetString()!).ToArray());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
