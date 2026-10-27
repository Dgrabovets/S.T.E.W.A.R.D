using Steward.Ideas.Models;
using Steward.Ideas.Storage;
using Steward.Reels.Common;
using Steward.Reels.Interfaces;
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
    /// Название и разбор берутся у Claude, а не из подписи
    /// </summary>
    [Fact]
    public async Task The_analysis_gives_the_idea_its_title_and_summary()
    {
        using var directory = new TempDirectory();
        var telegram = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/"));

        await Pipeline(directory, telegram, new FakeDownloader(), 7, out var ideas, new FakeAnalyzer(new IdeaAnalysis("Хук на звук", "Суть: звук после задачи"))).RunAsync(CancellationToken.None);

        var idea = Assert.Single(ideas.LoadAll());
        Assert.Equal(("Хук на звук", "Суть: звук после задачи"), (idea.Title, idea.Summary));
        Assert.Equal("Готово: Хук на звук", telegram.Sent.Last());
    }

    /// <summary>
    /// Уже обработанный рилс не разбирается заново: бот спрашивает, а ответ «Добавить ещё раз» создаёт вторую идею
    /// </summary>
    [Fact]
    public async Task A_repeated_link_is_asked_about_and_added_only_after_a_yes()
    {
        using var directory = new TempDirectory();
        var first = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/"));
        await Pipeline(directory, first, new FakeDownloader(), 7, out var ideas).RunAsync(CancellationToken.None);

        var downloader = new FakeDownloader();
        var second = new FakeTelegram(Message(2, 7, "https://www.instagram.com/reels/Abc/?igsh=x"));
        var asked = await Pipeline(directory, second, downloader, 7, out _).RunAsync(CancellationToken.None);

        Assert.Equal(0, downloader.Downloads);
        Assert.Equal(1, asked.Ignored);
        Assert.Contains("уже разбирали", second.Sent.Single(), StringComparison.Ordinal);
        Assert.Equal(["again:d2-1", "skip:d2-1"], second.Buttons);
        Assert.Single(ideas.LoadAll());

        var answer = new FakeTelegram(new TelegramMessage(3, 100, 7, "again:d2-1", "cb1"));
        var added = await Pipeline(directory, answer, downloader, 7, out _).RunAsync(CancellationToken.None);

        Assert.Equal(1, added.Accepted);
        Assert.Equal(["cb1"], answer.Answered);
        Assert.Equal(2, ideas.LoadAll().Count);
    }

    /// <summary>
    /// После удаления идеи ссылка всё равно считается обработанной, а ответ «Не надо» ничего не создаёт
    /// </summary>
    [Fact]
    public async Task A_deleted_idea_is_still_remembered_and_a_no_adds_nothing()
    {
        using var directory = new TempDirectory();
        await Pipeline(directory, new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/")), new FakeDownloader(), 7, out var ideas).RunAsync(CancellationToken.None);
        ideas.Delete("r1-1");

        var downloader = new FakeDownloader();
        var again = new FakeTelegram(Message(2, 7, "https://www.instagram.com/reel/Abc/"));
        await Pipeline(directory, again, downloader, 7, out _).RunAsync(CancellationToken.None);
        var skipped = new FakeTelegram(new TelegramMessage(3, 100, 7, "skip:d2-1", "cb2"));
        await Pipeline(directory, skipped, downloader, 7, out _).RunAsync(CancellationToken.None);

        Assert.Equal(0, downloader.Downloads);
        Assert.Empty(ideas.LoadAll());
        Assert.Equal("Хорошо, повторно не добавляю.", skipped.Sent.Single());
    }

    /// <summary>
    /// Следующий проход, в котором есть новые сообщения, удаляет ссылки и ответы прошлого, а без новых сообщений ничего не трогает
    /// </summary>
    [Fact]
    public async Task The_next_pass_removes_the_previous_messages_only_when_there_is_something_new()
    {
        using var directory = new TempDirectory();
        var first = new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/") with { MessageId = 501 });
        await Pipeline(directory, first, new FakeDownloader(), 7, out _).RunAsync(CancellationToken.None);
        Assert.Empty(first.Deleted);

        var quiet = new FakeTelegram();
        await Pipeline(directory, quiet, new FakeDownloader(), 7, out _).RunAsync(CancellationToken.None);
        Assert.Empty(quiet.Deleted);

        var second = new FakeTelegram(Message(2, 7, "https://www.instagram.com/reel/Zzz/") with { MessageId = 502 });
        await Pipeline(directory, second, new FakeDownloader(), 7, out _).RunAsync(CancellationToken.None);

        Assert.Equal([501L, 1001L, 1002L], second.Deleted.Order().ToArray());
    }

    /// <summary>
    /// Если новых сообщений нет, но с отправки прошло почти двое суток, бот сам удаляет прошлые, пока Telegram это позволяет
    /// </summary>
    [Fact]
    public async Task Old_messages_are_removed_even_when_nothing_new_arrives()
    {
        using var directory = new TempDirectory();
        await Pipeline(directory, new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/") with { MessageId = 501 }), new FakeDownloader(), 7, out _).RunAsync(CancellationToken.None);

        var early = new FakeTelegram();
        await Pipeline(directory, early, new FakeDownloader(), 7, out _, later: TimeSpan.FromHours(40)).RunAsync(CancellationToken.None);
        Assert.Empty(early.Deleted);

        var late = new FakeTelegram();
        await Pipeline(directory, late, new FakeDownloader(), 7, out _, later: TimeSpan.FromHours(47)).RunAsync(CancellationToken.None);
        Assert.Equal([501L, 1001L, 1002L], late.Deleted.Order().ToArray());
    }

    /// <summary>
    /// Нажатие кнопки, пришедшее во время ожидания, обрабатывается в той же проверке, а ошибка подтверждения нажатия её не роняет
    /// </summary>
    [Fact]
    public async Task A_button_pressed_while_waiting_is_handled_in_the_same_pass()
    {
        using var directory = new TempDirectory();
        await Pipeline(directory, new FakeTelegram(Message(1, 7, "https://www.instagram.com/reel/Abc/")), new FakeDownloader(), 7, out var ideas).RunAsync(CancellationToken.None);

        var telegram = new FakeTelegram(Message(2, 7, "https://www.instagram.com/reel/Abc/"));
        telegram.Late.Add(new TelegramMessage(3, 100, 7, "again:d2-1", "cb9"));
        var result = await Pipeline(directory, telegram, new FakeDownloader(), 7, out _, answerWait: 5).RunAsync(CancellationToken.None);

        Assert.Equal(1, result.Accepted);
        Assert.Equal(2, ideas.LoadAll().Count);
    }

    /// <summary>
    /// Собирает проверку бота с подделками и фиксированным временем
    /// </summary>
    /// <param name="directory">Папка для идей и номера сообщения</param>
    /// <param name="telegram">Подделка Telegram</param>
    /// <param name="downloader">Подделка загрузчика и расшифровки</param>
    /// <param name="allowedUserId">Разрешённый пользователь</param>
    /// <param name="ideas">Папка идей, в которую пишет проверка</param>
    /// <param name="analyzer">Разбор идеи или <see langword="null"/>, тогда разбора нет</param>
    /// <param name="answerWait">Сколько секунд ждать ответа на вопрос о повторе, по умолчанию не ждать</param>
    /// <param name="later">На сколько позже начала суток часы проверки, по умолчанию не позже</param>
    /// <returns>Проверка бота</returns>
    private static ReelsPipeline Pipeline(TempDirectory directory, FakeTelegram telegram, FakeDownloader downloader, long allowedUserId, out IdeaStore ideas, IIdeaAnalyzer? analyzer = null, TimeSpan? later = null, int answerWait = 0)
    {
        ideas = new IdeaStore(Path.Combine(directory.Path, "ideas"));

        return new ReelsPipeline(
            telegram,
            downloader,
            downloader,
            ideas,
            new OffsetStore(Path.Combine(directory.Path, "offset.txt")),
            new ReelsSettings { AllowedUserId = allowedUserId, AnswerWaitSeconds = answerWait },
            new FixedTime(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero) + (later ?? TimeSpan.Zero)),
            _ => { },
            analyzer ?? new FakeAnalyzer(null),
            new SeenStore(Path.Combine(directory.Path, "seen.txt"), Path.Combine(directory.Path, "pending.json")),
            new ChatLog(Path.Combine(directory.Path, "chat.json")));
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