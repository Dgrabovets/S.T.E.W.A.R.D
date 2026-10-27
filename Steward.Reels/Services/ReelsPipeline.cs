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
/// <param name="analyzer">Придумывает название идеи и пишет разбор</param>
/// <param name="seen">Помнит обработанные ссылки и вопросы о повторе</param>
public sealed class ReelsPipeline(
    ITelegramClient telegram,
    IReelDownloader downloader,
    ITranscriber transcriber,
    IdeaStore ideas,
    OffsetStore offsets,
    ReelsSettings settings,
    TimeProvider timeProvider,
    Action<string> log,
    IIdeaAnalyzer analyzer,
    SeenStore seen)
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

        if (message.CallbackId is not null)
            return await HandleAnswerAsync(message, cancellationToken);

        var links = ReelLinks.Find(message.Text);
        if (links.Count == 0)
        {
            await telegram.SendAsync(message.ChatId, "Пришлите ссылку на рилс Instagram, например https://www.instagram.com/reel/…", cancellationToken);

            return (0, 0, 1);
        }

        var accepted = 0;
        var failed = 0;
        var asked = 0;
        for (var index = 0; index < links.Count; index++)
        {
            var url = links[index];
            if (IsKnown(url))
            {
                await AskAboutRepeatAsync(message, url, $"d{message.UpdateId}-{index + 1}", cancellationToken);
                asked++;

                continue;
            }

            if (await ProcessAsync(message, url, $"r{message.UpdateId}-{index + 1}", cancellationToken))
                accepted++;
            else
                failed++;
        }

        return (accepted, failed, asked);
    }

    /// <summary>
    /// Разбирает нажатие кнопки под вопросом «добавить снова?»
    /// </summary>
    /// <param name="message">Нажатие: в тексте данные кнопки вида «again:метка» или «skip:метка»</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько идей создано, сколько рилсов не удалось и сколько раз нажатие пропущено</returns>
    private async Task<(int Accepted, int Failed, int Ignored)> HandleAnswerAsync(TelegramMessage message, CancellationToken cancellationToken)
    {
        await telegram.AnswerCallbackAsync(message.CallbackId!, cancellationToken);

        var parts = message.Text.Split(':', 2);
        var url = parts.Length == 2 ? seen.TakePending(parts[1]) : null;
        if (url is null)
            return (0, 0, 1);

        if (parts[0] != "again")
        {
            await telegram.SendAsync(message.ChatId, "Хорошо, повторно не добавляю.", cancellationToken);

            return (0, 0, 1);
        }

        return await ProcessAsync(message, url, $"r{message.UpdateId}-1", cancellationToken) ? (1, 0, 0) : (0, 1, 0);
    }

    /// <summary>
    /// Проверяет, обрабатывалась ли ссылка раньше: в списке обработанных или среди идей, которые уже лежат в папке
    /// </summary>
    /// <param name="url">Ссылка на рилс</param>
    /// <returns>
    /// <see langword="true"/>, если по этому рилсу идея уже была
    /// </returns>
    private bool IsKnown(string url)
    {
        var key = ReelLinks.Key(url);

        return seen.Keys().Contains(key) || ideas.LoadAll().Any(idea => ReelLinks.Key(idea.SourceUrl) == key);
    }

    /// <summary>
    /// Спрашивает в чате, добавлять ли рилс повторно, и запоминает вопрос до нажатия кнопки
    /// </summary>
    /// <param name="message">Сообщение со ссылкой</param>
    /// <param name="url">Ссылка на рилс</param>
    /// <param name="token">Метка вопроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача, которая завершается, когда вопрос отправлен</returns>
    private async Task AskAboutRepeatAsync(TelegramMessage message, string url, string token, CancellationToken cancellationToken)
    {
        seen.AddPending(token, url);
        log($"Повтор: {url}");
        await telegram.SendWithButtonsAsync(message.ChatId, $"Этот рилс уже разбирали: {url}\nДобавить ещё раз?", [("Добавить ещё раз", $"again:{token}"), ("Не надо", $"skip:{token}")], cancellationToken);
    }

    /// <summary>
    /// Превращает один рилс в идею. Повторное сообщение с тем же номером идею не дублирует. Уже обработанную ссылку сюда присылают только после согласия человека
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
            var analysis = await analyzer.AnalyzeAsync(reel.Caption, transcript, cancellationToken);
            if (analysis is null)
                log("Claude не разобрал идею: название взято из подписи");

            var idea = new Idea
            {
                Id = id,
                CreatedAt = timeProvider.GetLocalNow(),
                SourceUrl = url,
                Title = analysis?.Title ?? IdeaTitle.Build(reel.Caption, transcript, reel.Author),
                Summary = analysis?.Summary ?? string.Empty,
                Author = reel.Author,
                Caption = reel.Caption,
                Transcript = transcript,
            };

            ideas.Save(idea);
            seen.Add(ReelLinks.Key(url));
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