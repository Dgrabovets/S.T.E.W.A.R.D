using System.Net;
using System.Text;

namespace Steward.Reels.Tests.Fakes;

/// <summary>
/// Обработчик HTTP, который запоминает запрос и отвечает заданным кодом и телом
/// </summary>
/// <param name="status">Код ответа</param>
/// <param name="body">Тело ответа</param>
public sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    /// <summary>
    /// Адрес последнего запроса
    /// </summary>
    public string? RequestUrl { get; private set; }

    /// <summary>
    /// Тело последнего запроса
    /// </summary>
    public string RequestBody { get; private set; } = string.Empty;

    /// <summary>
    /// Запоминает запрос и отвечает
    /// </summary>
    /// <param name="request">Запрос клиента</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ответ с заданными кодом и телом</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestUrl = request.RequestUri?.ToString();
        RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}