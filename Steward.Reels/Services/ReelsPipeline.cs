using Steward.Ideas.Models;
using Steward.Ideas.Storage;
using Steward.Reels.Common;
using Steward.Reels.Interfaces;
using Steward.Reels.Models;

namespace Steward.Reels.Services;

/// <summary>
/// Одна проверка бота: читает новые сообщения, превращает ссылки на рилсы в идеи и отвечает в Telegram
/// </summary>
/// <param name="telegram">Бот Telegram</param>
/// <param name="downloader">Скачивает рилсы</param>
/// <param name="transcriber">Расшифровывает речь</param>
/// <param name="ideas">Папка идей</param>
/// <param name="offsets">Помнит, какие сообщения уже прочитаны</param>
/// <param name="settings">Настройки бота</param>
/// <param name="timeProvider">Часы: время создания идеи</param>
/// <param name="log">Принимает строки лога</param>
public sealed class ReelsPipeline(
    ITelegramClient telegram,
    IReelDownloader downloader,
    ITranscriber transcriber,
    IdeaStore ideas,
    OffsetStore offsets,
    ReelsSettings settings,
    TimeProvider timeProvider,
    Action<string> log)
{
    /// <summary>
    /// Читает сообщения и обрабатывает их по порядку. Номер прочитанного сохраняется после каждого сообщения
    /// </summary>
    /// <param name="cancellationToken">Отменяет проверку</param>
    /// <returns>Сколько идей создано, сколько рилсов не удалось и сколько идей ждёт решения</returns>
    public async Task<ReelsResult> RunAsync(CancellationToken cancellationToken)
    {
        var accepted = 0;
        var failed = 0;
        var ignored = 0;

        foreach (var message in (await telegram.GetMessagesAsync(offsets.Read(), cancellationToken)).OrderBy(item => item.UpdateId))
        {
            var (ok, bad, skipped) = await HandleAsync(message, cancellationToken);
            accepted += ok;
            failed += bad;
            ignored += skipped;

            // Сохраняем после каждого сообщения: если следующее упадёт, это не прочитается второй раз
            offsets.Write(message.UpdateId + 1);
        }

        return new ReelsResult(accepted, failed, ignored, ideas.LoadAll().Count(idea => idea.State == IdeaStateEnum.New));
    }

    /// <summary>
    /// Разбирает одно сообщение
    /// </summary>
    /// <param name="message">Сообщение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько в нём идей создано, сколько рилсов не удалось и сколько раз сообщение пропущено</returns>
    private async Task<(int Accepted, int Failed, int Ignored)> HandleAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        if (settings.AllowedUserId == 0)
        {
            log($"Сообщение от пользователя {message.UserId}: идентификатор в настройках не задан");
            await telegram.SendAsync(message.ChatId, $"Ваш Telegram id {message.UserId}. Впишите его в AllowedUserId в настройках бота, дальше бот будет принимать рилсы только от вас.", cancellationToken);

            return (0, 0, 1);
        }

        if (message.UserId != settings.AllowedUserId)
        {
            log($"Сообщение от чужого пользователя {message.UserId} пропущено");

            return (0, 0, 1);
        }

        var links = ReelLinks.Find(message.Text);
        if (links.Count == 0)
        {
            await telegram.SendAsync(message.ChatId, "Пришлите ссылку на рилс Instagram, например https://www.instagram.com/reel/…", cancellationToken);

            return (0, 0, 1);
        }

        var accepted = 0;
        var failed = 0;
        for (var index = 0; index < links.Count; index++)
        {
            if (await ProcessAsync(message, links[index], $"r{message.UpdateId}-{index + 1}", cancellationToken))
                accepted++;
            else
                failed++;
        }

        return (accepted, failed, 0);
    }

    /// <summary>
    /// Превращает один рилс в идею. Повторное сообщение с тем же номером идею не дублирует
    /// </summary>
    /// <param name="message">Сообщение со ссылкой</param>
    /// <param name="url">Ссылка на рилс</param>
    /// <param name="id">Идентификатор идеи</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>
    /// <see langword="true"/>, если идея создана или уже была, <see langword="false"/>, если рилс не удалось обработать
    /// </returns>
    private async Task<bool> ProcessAsync(TelegramMessage message, string url, string id, CancellationToken cancellationToken)
    {
        if (ideas.Exists(id))
            return true;

        await telegram.SendAsync(message.ChatId, "Принял, разбираю. Это займёт пару минут.", cancellationToken);
        try
        {
            var reel = await downloader.DownloadAsync(url, id, cancellationToken);
            var transcript = await transcriber.TranscribeAsync(reel.MediaPath, cancellationToken);
            var idea = new Idea
            {
                Id = id,
                CreatedAt = timeProvider.GetLocalNow(),
                SourceUrl = url,
                Title = IdeaTitle.Build(reel.Caption, transcript, reel.Author),
                Author = reel.Author,
                Caption = reel.Caption,
                Transcript = transcript,
            };

            ideas.Save(idea);
            log($"Идея: {idea.Title} ← {url}");
            await telegram.SendAsync(message.ChatId, $"Готово: {idea.Title}", cancellationToken);

            return true;
        }
        catch (ReelException exception)
        {
            log($"Не получилось: {url}: {exception.Message}");
            await telegram.SendAsync(message.ChatId, $"Не получилось: {exception.Message}", cancellationToken);

            return false;
        }
        finally
        {
            downloader.Clean(id);
        }
    }
}