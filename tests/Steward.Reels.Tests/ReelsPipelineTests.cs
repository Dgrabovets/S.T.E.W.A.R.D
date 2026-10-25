using Steward.Ideas.Models;
using Steward.Ideas.Storage;
using Steward.Reels.Common;
using Steward.Reels.Models;
using Steward.Reels.Services;
using Steward.Reels.Tests.Fakes;

namespace Steward.Reels.Tests;

/// <summary>
/// Проверяет проверку бота целиком: кого слушаем, что превращается в идею и что отвечаем
/// </summary>
public class ReelsPipelineTests
{
    /// <summary>
    /// Ссылка на рилс от хозяина превращается в идею и ответ «Готово»
    /// </summary>
    [Fact]
    public async Task A_reel_link_from_the_owner_becomes_an_idea()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/"));
        var downloader = new FakeDownloader();

        var result = await Pipeline(directory, telegram, downloader, 7, out var ideas).RunAsync(CancellationToken.None);

        var idea = Assert.Single(ideas.LoadAll());
        Assert.Equal(("r1-1", "Подпись рилса", IdeaStateEnum.New), (idea.Id, idea.Title, idea.State));
        Assert.Equal("Первая мысль. Вторая мысль.", idea.Transcript);
        Assert.Equal(new ReelsResult(1, 0, 0, 1), result);
        Assert.Equal("Готово: Подпись рилса", telegram.Sent.Last());
        Assert.Equal(["r1-1"], downloader.Cleaned);
    }

    /// <summary>
    /// Пока в настройках нет id, бот сообщает отправителю его номер и идей не создаёт
    /// </summary>
    [Fact]
    public async Task Without_an_allowed_id_the_sender_is_told_their_id()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 99, "https://www.instagram.com/reel/Abc/"));

        var result = await Pipeline(directory, telegram, new FakeDownloader(), 0, out var ideas).RunAsync(CancellationToken.None);

        Assert.Empty(ideas.LoadAll());
        Assert.Contains("99", telegram.Sent.Single(), StringComparison.Ordinal);
        Assert.Equal(1, result.Ignored);
    }

    /// <summary>
    /// Сообщение чужого пользователя молча пропускается
    /// </summary>
    [Fact]
    public async Task A_foreign_user_is_ignored_silently()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 99, "https://www.instagram.com/reel/Abc/"));
        var downloader = new FakeDownloader();

        var result = await Pipeline(directory, telegram, downloader, 7, out var ideas).RunAsync(CancellationToken.None);

        Assert.Empty(telegram.Sent);
        Assert.Empty(ideas.LoadAll());
        Assert.Equal(0, downloader.Downloads);
        Assert.Equal(1, result.Ignored);
    }

    /// <summary>
    /// Сообщение без ссылки получает подсказку
    /// </summary>
    [Fact]
    public async Task A_message_without_a_link_gets_a_hint()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 7, "привет"));

        await Pipeline(directory, telegram, new FakeDownloader(), 7, out var ideas).RunAsync(CancellationToken.None);

        Assert.Contains("ссылку", telegram.Sent.Single(), StringComparison.Ordinal);
        Assert.Empty(ideas.LoadAll());
    }

    /// <summary>
    /// Если рилс не скачался, бот отвечает причиной, идеи нет, файлы всё равно чистятся
    /// </summary>
    [Fact]
    public async Task A_failed_download_is_reported_and_cleaned_up()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/"));
        var downloader = new FakeDownloader { FailWith = "нужен вход" };

        var result = await Pipeline(directory, telegram, downloader, 7, out var ideas).RunAsync(CancellationToken.None);

        Assert.Empty(ideas.LoadAll());
        Assert.Equal("Не получилось: нужен вход", telegram.Sent.Last());
        Assert.Equal((0, 1), (result.Accepted, result.Failed));
        Assert.Equal(["r1-1"], downloader.Cleaned);
    }

    /// <summary>
    /// Прочитанные сообщения не читаются второй раз, а рилс с уже созданной идеей не скачивается заново
    /// </summary>
    [Fact]
    public async Task Messages_are_read_once_and_an_existing_idea_is_not_repeated()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/"));
        var downloader = new FakeDownloader();
        var pipeline = Pipeline(directory, telegram, downloader, 7, out _);

        await pipeline.RunAsync(CancellationToken.None);
        await pipeline.RunAsync(CancellationToken.None);

        Assert.Equal(2, telegram.LastOffset);
        Assert.Equal(1, downloader.Downloads);
    }

    /// <summary>
    /// Две ссылки в одном сообщении дают две идеи с разными номерами
    /// </summary>
    [Fact]
    public async Task Two_links_make_two_ideas()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(5, 7, "https://www.instagram.com/reel/A1/ https://www.instagram.com/reel/B2/"));

        await Pipeline(directory, telegram, new FakeDownloader(), 7, out var ideas).RunAsync(CancellationToken.None);

        Assert.Equal(["r5-1", "r5-2"], ideas.LoadAll().Select(idea => idea.Id).Order().ToArray());
    }

    /// <summary>
    /// Собирает проверку бота с подделками и фиксированным временем
    /// </summary>
    /// <param name="directory">Папка для идей и номера сообщения</param>
    /// <param name="telegram">Подделка Telegram</param>
    /// <param name="downloader">Подделка загрузчика и расшифровки</param>
    /// <param name="allowedUserId">Разрешённый пользователь</param>
    /// <param name="ideas">Папка идей, в которую пишет проверка</param>
    /// <returns>Проверка бота</returns>
    private static ReelsPipeline Pipeline(TempDirectory directory, FakeTelegram telegram, FakeDownloader downloader, long allowedUserId, out IdeaStore ideas)
    {
        ideas = new IdeaStore(Path.Combine(directory.Path, "ideas"));

        return new ReelsPipeline(
            telegram,
            downloader,
            downloader,
            ideas,
            new OffsetStore(Path.Combine(directory.Path, "offset.txt")),
            new ReelsSettings { AllowedUserId = allowedUserId },
            new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero)),
            _ => { });
    }

    /// <summary>
    /// Собирает сообщение в чате с отправителем
    /// </summary>
    /// <param name="updateId">Номер обновления</param>
    /// <param name="userId">Отправитель</param>
    /// <param name="text">Текст</param>
    /// <returns>Сообщение</returns>
    private static TelegramMessage Message(long updateId, long userId, string text) => new(updateId, 100, userId, text);
}