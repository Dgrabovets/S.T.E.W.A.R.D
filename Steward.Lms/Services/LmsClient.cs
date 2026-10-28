using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Steward.Lms.Common;
using Steward.Lms.Models;

namespace Steward.Lms.Services;

/// <summary>
/// Заходит в LMS по логину и паролю обычными запросами, без браузера, и читает курсы и записи вебинаров
/// </summary>
public sealed partial class LmsClient : IDisposable
{
    /// <summary>
    /// Настройки: адрес сайта, логин и пароль
    /// </summary>
    private readonly LmsSettings _settings;

    /// <summary>
    /// Клиент с общими cookies: вход один раз, дальше все запросы идут в этом сеансе
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Ключ сеанса Moodle для запросов к его внутреннему API. Появляется после входа
    /// </summary>
    private string _sesskey = string.Empty;

    /// <summary>
    /// Создаёт клиента. Вход выполняется отдельным вызовом
    /// </summary>
    /// <param name="settings">Настройки входа</param>
    public LmsClient(LmsSettings settings)
    {
        _settings = settings;
        _http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true }) { Timeout = TimeSpan.FromSeconds(60) };

        // Без заголовка браузера LMS отвечает 403
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Steward/1.0");
    }

    /// <summary>
    /// Входит в LMS: берёт со страницы входа одноразовый токен и отправляет его вместе с логином и паролем
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача, которая завершается после входа</returns>
    /// <exception cref="LmsException">Не заданы логин или пароль, либо LMS не пустила</exception>
    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Login) || _settings.Login.StartsWith('<') || string.IsNullOrWhiteSpace(_settings.Password) || _settings.Password.StartsWith('<'))
            throw new LmsException("Не заданы Login и Password в Steward.Lms/lmssettings.json");

        var loginUrl = Url("/login/index.php");
        var page = await _http.GetStringAsync(loginUrl, cancellationToken);
        var token = LoginToken().Match(page).Groups[1].Value;

        using var response = await _http.PostAsync(loginUrl, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = _settings.Login,
            ["password"] = _settings.Password,
            ["logintoken"] = token,
        }), cancellationToken);

        if (response.RequestMessage?.RequestUri?.AbsolutePath.Contains("/login/", StringComparison.Ordinal) == true)
            throw new LmsException("LMS не пустила: проверь логин и пароль в Steward.Lms/lmssettings.json");

        var home = await response.Content.ReadAsStringAsync(cancellationToken);
        _sesskey = SessKey().Match(home).Groups[1].Value;
    }

    /// <summary>
    /// Читает все курсы из личного кабинета
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Курсы в порядке названий</returns>
    /// <exception cref="LmsException">Вход не выполнен или LMS ответила не так, как обычно</exception>
    public async Task<IReadOnlyList<LmsCourse>> CoursesAsync(CancellationToken cancellationToken)
    {
        if (_sesskey.Length == 0)
            throw new LmsException("LMS не отдала ключ сеанса: войти не получилось");

        const string method = "core_course_get_enrolled_courses_by_timeline_classification";
        var body = JsonSerializer.Serialize(new[]
        {
            new { index = 0, methodname = method, args = new { offset = 0, limit = 0, classification = "all", sort = "fullname", customfieldname = string.Empty, customfieldvalue = string.Empty } },
        });

        using var response = await _http.PostAsync(Url($"/lib/ajax/service.php?sesskey={_sesskey}&info={method}"), new StringContent(body, Encoding.UTF8, "application/json"), cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        try
        {
            return
            [
                .. document.RootElement[0].GetProperty("data").GetProperty("courses").EnumerateArray()
                    .Select(course => new LmsCourse(course.GetProperty("id").GetInt32(), course.GetProperty("fullname").GetString() ?? string.Empty)),
            ];
        }
        catch (Exception exception) when (exception is KeyNotFoundException or InvalidOperationException)
        {
            throw new LmsException("LMS вернула список курсов в незнакомом виде");
        }
    }

    /// <summary>
    /// Читает прошедшие вебинары курса со всех его страниц «Ссылка на занятия МТС Линк»
    /// </summary>
    /// <param name="course">Курс</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Вебинары с записью. Пусто, если у курса нет такой страницы</returns>
    public async Task<IReadOnlyList<Webinar>> WebinarsAsync(LmsCourse course, CancellationToken cancellationToken)
    {
        var courseHtml = await _http.GetStringAsync(Url($"/course/view.php?id={course.Id}"), cancellationToken);
        var webinars = new List<Webinar>();
        foreach (var pageId in MtsPages().Matches(courseHtml).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal))
            webinars.AddRange(WebinarParser.Parse(await _http.GetStringAsync(Url($"/mod/mtslinkrnhgs/view.php?id={pageId}"), cancellationToken)));

        return [.. webinars.DistinctBy(webinar => webinar.SessionId)];
    }

    /// <summary>
    /// Читает задания и тесты курса с их состоянием: сданы они или нет
    /// </summary>
    /// <param name="course">Курс</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Работы курса. Пусто, если в курсе нет ни заданий, ни тестов</returns>
    public async Task<IReadOnlyList<LmsActivity>> ActivitiesAsync(LmsCourse course, CancellationToken cancellationToken)
    {
        var assignments = await _http.GetStringAsync(Url($"/mod/assign/index.php?id={course.Id}"), cancellationToken);
        var quizzes = await _http.GetStringAsync(Url($"/mod/quiz/index.php?id={course.Id}"), cancellationToken);

        return [.. ActivityParser.ParseAssignments(assignments, course.Id, course.Subject), .. ActivityParser.ParseQuizzes(quizzes, course.Id, course.Subject)];
    }

    /// <summary>
    /// Читает страницу LMS как текст
    /// </summary>
    /// <param name="url">Полный адрес страницы</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>HTML страницы</returns>
    public Task<string> PageAsync(string url, CancellationToken cancellationToken) => _http.GetStringAsync(url, cancellationToken);

    /// <summary>
    /// Скачивает файл с сайта LMS под текущим входом
    /// </summary>
    /// <param name="url">Полный адрес файла</param>
    /// <param name="path">Куда записать</param>
    /// <param name="maxBytes">Больше стольких байт не качается</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>
    /// <see langword="true"/>, если файл скачан
    /// </returns>
    public async Task<bool> DownloadAsync(string url, string path, long maxBytes, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > maxBytes || response.Content.Headers.ContentType?.MediaType is "text/html")
            return false;

        await using var output = File.Create(path);
        await response.Content.CopyToAsync(output, cancellationToken);

        return true;
    }

    /// <summary>
    /// Освобождает HTTP-клиент
    /// </summary>
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Собирает полный адрес на сайте LMS
    /// </summary>
    /// <param name="path">Путь с начальным слэшем</param>
    /// <returns>Адрес целиком</returns>
    private string Url(string path) => _settings.BaseUrl.TrimEnd('/') + path;

    /// <summary>
    /// Одноразовый токен формы входа
    /// </summary>
    /// <returns>Регулярное выражение с одной группой</returns>
    [GeneratedRegex("name=\"logintoken\"\\s+value=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex LoginToken();

    /// <summary>
    /// Ключ сеанса в коде страницы
    /// </summary>
    /// <returns>Регулярное выражение с одной группой</returns>
    [GeneratedRegex("\"sesskey\":\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex SessKey();

    /// <summary>
    /// Ссылки курса на страницы занятий МТС Линк
    /// </summary>
    /// <returns>Регулярное выражение с номером страницы</returns>
    [GeneratedRegex("/mod/mtslinkrnhgs/view\\.php\\?id=(\\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex MtsPages();
}
