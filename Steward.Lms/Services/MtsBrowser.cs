using System.Text.Json;
using Microsoft.Playwright;
using Steward.Lms.Common;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Управляет отдельным браузером для MTS Link: человек входит в него один раз, вход сохраняется в файл вместе с cookies сеанса, а дальше программа работает в нём без окна
/// </summary>
/// <param name="settings">Настройки: куда ставить браузер и где лежит файл входа</param>
/// <param name="repoRoot">Корень репозитория: от него считаются пути по умолчанию</param>
public sealed class MtsBrowser(LmsSettings settings, string repoRoot)
{
    /// <summary>
    /// Адрес входа в MTS Link
    /// </summary>
    private const string SIGN_IN_URL = "https://my.mts-link.ru/signin";

    /// <summary>
    /// Адрес, который отвечает «найден», только когда в сеансе выполнен вход
    /// </summary>
    private const string SESSION_URL = "https://gw.mts-link.ru/api/login";

    /// <summary>
    /// Папка, куда Playwright ставит браузеры
    /// </summary>
    public string BrowsersPath => string.IsNullOrWhiteSpace(settings.BrowsersPath) ? Path.GetFullPath(Path.Combine(repoRoot, "..", "Playwright")) : settings.BrowsersPath;

    /// <summary>
    /// Файл с сохранённым входом: cookies и данные сайта. Это ключ к аккаунту, в репозиторий он не попадает
    /// </summary>
    public string StatePath => string.IsNullOrWhiteSpace(settings.MtsStatePath) ? Path.Combine(repoRoot, "data", "lms", "mts-state.json") : settings.MtsStatePath;

    /// <summary>
    /// Ставит браузер Chromium для Playwright в <see cref="BrowsersPath"/>
    /// </summary>
    /// <returns>Код завершения установщика, ноль при успехе</returns>
    public int Install()
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPath);

        return Program.Install();
    }

    /// <summary>
    /// Открывает окно браузера на странице входа и ждёт, пока человек войдёт. Вход считается выполненным, когда MTS Link узнаёт пользователя, и тогда он сохраняется в файл
    /// </summary>
    /// <param name="wait">Сколько ждать входа</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>
    /// <see langword="true"/>, если вход выполнен и сохранён, <see langword="false"/>, если окно закрыли или время вышло
    /// </returns>
    public async Task<bool> SignInAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(headless: false, restore: false);
        var page = await session.Context.NewPageAsync();
        await page.GotoAsync(SIGN_IN_URL);

        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (page.IsClosed)
                return false;

            if (await IsSignedInAsync(page))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
                await session.Context.StorageStateAsync(new BrowserContextStorageStateOptions { Path = StatePath });

                return true;
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Открывает запись без окна и собирает адреса запросов страницы к расшифровке: нужно, чтобы узнать, откуда брать полный текст
    /// </summary>
    /// <param name="recordUrl">Адрес записи на my.mts-link.ru</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Строки «код ответа, метод и адрес» для запросов к записи и расшифровке</returns>
    public async Task<IReadOnlyList<string>> ProbeAsync(string recordUrl, CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(headless: true, restore: true);
        var page = await session.Context.NewPageAsync();
        var seen = new List<string>();
        page.Response += (_, response) =>
        {
            if (response.Url.Contains("transcript", StringComparison.OrdinalIgnoreCase) || response.Url.Contains("record-files", StringComparison.OrdinalIgnoreCase) || response.Url.Contains("/api/login", StringComparison.OrdinalIgnoreCase))
                seen.Add($"{response.Status} {response.Request.Method} {response.Url}");
        };

        await page.GotoAsync(recordUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);

        return seen;
    }

    /// <summary>
    /// Забирает расшифровки записей: открывает каждую запись без окна под сохранённым входом и читает ответы MTS Link
    /// </summary>
    /// <param name="records">Адреса записей на my.mts-link.ru по ключу занятия</param>
    /// <param name="save">Вызывается для каждой записи сразу после чтения, чтобы при сбое на следующей прочитанное не пропало</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько расшифровок получено. Записи без текста пропускаются</returns>
    /// <exception cref="LmsException">Сохранённого входа нет или MTS Link его не принимает</exception>
    public async Task<int> FetchAsync(IReadOnlyDictionary<string, string> records, Func<string, MtsTranscript, Task> save, CancellationToken cancellationToken)
    {
        if (!File.Exists(StatePath))
            throw new LmsException("Нет сохранённого входа в MTS Link: выполни команду login");

        await using var session = await OpenAsync(headless: true, restore: true);
        var page = await session.Context.NewPageAsync();
        var received = 0;

        foreach (var (lessonId, recordUrl) in records)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await page.GotoAsync(recordUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            if (!await IsSignedInAsync(page))
                throw new LmsException("MTS Link больше не помнит вход: выполни команду login и войди заново");

            var sessionId = recordUrl[(recordUrl.LastIndexOf('/') + 1)..];
            using var record = await GetJsonAsync(page, $"https://gw.mts-link.ru/api/eventsessions/{sessionId}/record?withoutCuts=false");
            var fileId = record.RootElement.GetProperty("id").GetInt64();

            using var summary = await GetJsonAsync(page, $"https://gw.mts-link.ru/api/record-files/{fileId}/transcript/summary");
            var items = summary.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
            if (items.Length == 0)
                continue;

            var ids = string.Join('&', items.Select((item, index) => $"transcriptIds%5B{index}%5D={item.GetProperty("transcriptId").GetInt64()}"));
            using var lines = await GetJsonAsync(page, $"https://gw.mts-link.ru/api/record-files/{fileId}/transcript?{ids}&perPage=10000");

            var parsed = lines.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
                .Select(line => new TranscriptLine(TimeSpan.FromSeconds(line.GetProperty("relativeTimeOnRecord").GetDouble()), line.GetProperty("nickname").GetString() ?? string.Empty, line.GetProperty("text").GetString() ?? string.Empty))
                .OrderBy(line => line.Offset)
                .ToArray();

            await save(lessonId, new MtsTranscript(string.Join("\n\n", items.Select(item => item.GetProperty("summary").GetString()).Where(text => !string.IsNullOrWhiteSpace(text))), parsed));
            received++;
        }

        return received;
    }

    /// <summary>
    /// Читает ответ MTS Link на запрос из страницы, где выполнен вход
    /// </summary>
    /// <param name="page">Открытая запись на my.mts-link.ru</param>
    /// <param name="url">Адрес запроса к gw.mts-link.ru</param>
    /// <returns>Разобранный ответ</returns>
    /// <exception cref="LmsException">Сервер ответил ошибкой</exception>
    private static async Task<JsonDocument> GetJsonAsync(IPage page, string url)
    {
        var result = await page.EvaluateAsync<string>("url => fetch(url, { credentials: 'include' }).then(async response => response.status + ' ' + (await response.text()))", url);
        if (!result.StartsWith("200 ", StringComparison.Ordinal))
            throw new LmsException($"MTS Link ответил {result[..Math.Min(3, result.Length)]} на запрос расшифровки");

        return JsonDocument.Parse(result[4..]);
    }

    /// <summary>
    /// Читает ответы MTS Link на запросы из открытой записи: нужно, чтобы узнать устройство ответов
    /// </summary>
    /// <param name="recordUrl">Адрес записи на my.mts-link.ru</param>
    /// <param name="requests">Адреса запросов к gw.mts-link.ru</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Для каждого запроса код ответа, длина и начало тела</returns>
    public async Task<IReadOnlyList<string>> GetAsync(string recordUrl, IEnumerable<string> requests, CancellationToken cancellationToken)
    {
        await using var session = await OpenAsync(headless: true, restore: true);
        var page = await session.Context.NewPageAsync();
        await page.GotoAsync(recordUrl, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        cancellationToken.ThrowIfCancellationRequested();

        var lines = new List<string>();
        foreach (var url in requests)
        {
            var result = await page.EvaluateAsync<string>("url => fetch(url, { credentials: 'include' }).then(async response => response.status + ' ' + (await response.text()))", url);
            lines.Add($"{url} -> {result.Length} знаков: {result[..Math.Min(result.Length, 600)]}");
        }

        return lines;
    }

    /// <summary>
    /// Запускает Chromium и создаёт контекст. Сохранённый вход подставляется, если он есть и нужен
    /// </summary>
    /// <param name="headless">Без окна</param>
    /// <param name="restore">Подставить сохранённый вход из <see cref="StatePath"/></param>
    /// <returns>Сеанс браузера: закрывается через <c>await using</c></returns>
    private async Task<MtsSession> OpenAsync(bool headless, bool restore)
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", BrowsersPath);

        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
        var options = new BrowserNewContextOptions { ViewportSize = headless ? new ViewportSize { Width = 1280, Height = 800 } : ViewportSize.NoViewport };
        if (restore && File.Exists(StatePath))
            options.StorageStatePath = StatePath;

        return new MtsSession(playwright, browser, await browser.NewContextAsync(options));
    }

    /// <summary>
    /// Спрашивает у MTS Link из страницы, узнаёт ли он пользователя
    /// </summary>
    /// <param name="page">Страница на my.mts-link.ru</param>
    /// <returns>
    /// <see langword="true"/>, если сервер отвечает «пользователь найден»
    /// </returns>
    private static async Task<bool> IsSignedInAsync(IPage page)
    {
        try
        {
            var status = await page.EvaluateAsync<int>($"fetch('{SESSION_URL}', {{ credentials: 'include' }}).then(response => response.status)");

            return status == 200;
        }
        catch (PlaywrightException)
        {
            // Страница ещё переходит на другой адрес: проверим на следующем круге
            return false;
        }
    }

    /// <summary>
    /// Запущенный браузер с одним контекстом: всё закрывается вместе
    /// </summary>
    /// <param name="playwright">Движок Playwright</param>
    /// <param name="browser">Запущенный браузер</param>
    /// <param name="context">Контекст с cookies сеанса</param>
    private sealed class MtsSession(IPlaywright playwright, IBrowser browser, IBrowserContext context) : IAsyncDisposable
    {
        /// <summary>
        /// Контекст браузера
        /// </summary>
        public IBrowserContext Context => context;

        /// <summary>
        /// Закрывает контекст, браузер и движок
        /// </summary>
        /// <returns>Задача, которая завершается после закрытия</returns>
        public async ValueTask DisposeAsync()
        {
            await context.CloseAsync();
            await browser.CloseAsync();
            playwright.Dispose();
        }
    }
}
