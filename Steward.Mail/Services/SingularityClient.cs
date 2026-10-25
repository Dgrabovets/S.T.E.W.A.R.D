using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Steward.Mail.Services;

/// <summary>
/// Создание задач через REST API SingularityApp
/// </summary>
public sealed class SingularityClient : ISingularityClient, IDisposable
{
    /// <summary>
    /// Клиент, у которого базовый адрес — API SingularityApp, а в заголовке токен
    /// </summary>
    private readonly HttpClient _http;

    /// <summary>
    /// Создаёт клиент, авторизованный токеном из настроек
    /// </summary>
    /// <param name="settings">Адрес API и токен доступа</param>
    public SingularityClient(SingularitySettings settings)
        : this(CreateHttp(settings))
    {
    }

    /// <summary>
    /// Создаёт клиент на готовом HTTP-клиенте: его адрес и заголовок задаёт вызывающий
    /// </summary>
    /// <param name="http">HTTP-клиент, которым владеет создаваемый объект</param>
    public SingularityClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Создаёт задачу запросом POST /v2/task
    /// </summary>
    /// <param name="request">Поля новой задачи</param>
    /// <param name="cancellationToken">Токен отмены запроса</param>
    /// <returns>Задача, которая завершается, когда SingularityApp принимает запрос</returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync("v2/task", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>
    /// Освобождает HTTP-клиент
    /// </summary>
    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Собирает HTTP-клиент с базовым адресом API и токеном в заголовке
    /// </summary>
    /// <param name="settings">Адрес API и токен доступа</param>
    /// <returns>Клиент, готовый к запросам</returns>
    private static HttpClient CreateHttp(SingularitySettings settings)
    {
        var http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);

        return http;
    }
}