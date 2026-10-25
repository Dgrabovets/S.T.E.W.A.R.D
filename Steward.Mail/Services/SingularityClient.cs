using System.Net.Http.Headers;
using System.Net.Http.Json;
using Steward.Mail.Common;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Mail.Services;

/// <summary>
/// Создание задач через REST API SingularityApp
/// </summary>
public sealed class SingularityClient : ISingularityClient, IDisposable
{
    #region Fields

    /// <summary>
    /// Клиент, у которого базовый адрес — API SingularityApp, а в заголовке токен
    /// </summary>
    private readonly HttpClient _http;

    #endregion

    #region Constructors

    /// <summary>
    /// Создаёт клиент, авторизованный токеном из настроек
    /// </summary>
    /// <param name="settings">
    /// Адрес API и токен доступа
    /// </param>
    public SingularityClient(SingularitySettings settings)
    {
        _http = new HttpClient { BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Создаёт задачу запросом POST /v2/task
    /// </summary>
    /// <param name="request">
    /// Поля новой задачи
    /// </param>
    /// <param name="ct">
    /// Токен отмены запроса
    /// </param>
    /// <returns>
    /// Задача, которая завершается, когда SingularityApp принимает запрос
    /// </returns>
    /// <exception cref="HttpRequestException">
    /// SingularityApp ответил ошибкой. В тексте код ответа и тело
    /// </exception>
    public async Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("v2/task", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"SingularityApp ответил {(int)response.StatusCode}: {body}");
        }
    }

    /// <summary>
    /// Освобождает HTTP-клиент
    /// </summary>
    public void Dispose() => _http.Dispose();

    #endregion
}
