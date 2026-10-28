using System.IO;
using System.Text.Json;
using Steward.Models;
using Steward.ViewModels;

namespace Steward.Hosting;

/// <summary>
/// Смотрит, заданы ли настройки процессов: заполнены ли токены и пароли, есть ли сохранённый вход. Самих секретов не читает и не показывает, только отвечает «задано» или «не задано»
/// </summary>
public static class SettingsProbe
{
    /// <summary>
    /// Путь к файлу настроек почты рядом с собранной программой
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Полный путь</returns>
    public static string MailFile(string root) => Path.Combine(root, "Steward.Mail", "publish", "appsettings.json");

    /// <summary>
    /// Путь к файлу настроек бота рилсов
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Полный путь</returns>
    public static string ReelsFile(string root) => Path.Combine(root, "Steward.Reels", "publish", "appsettings.json");

    /// <summary>
    /// Путь к файлу настроек выгрузки занятий
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Полный путь</returns>
    public static string LmsFile(string root) => Path.Combine(root, "Steward.Lms", "publish", "lmssettings.json");

    /// <summary>
    /// Состояние настроек почты: файл, пароль приложения и токен SingularityApp
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Чипы для страницы «Почта»</returns>
    public static IReadOnlyList<StatusChipViewModel> Mail(string root)
    {
        using var document = Read(MailFile(root));

        return
        [
            Chip(document is not null, "Файл настроек", "найден", "не найден"),
            Chip(Filled(document, "Mail", "AppPassword"), "Пароль приложения", "задан", "не задан"),
            Chip(Filled(document, "Singularity", "Token"), "Токен Singularity", "задан", "не задан"),
        ];
    }

    /// <summary>
    /// Состояние бота рилсов: токен, владелец и вход в Instagram
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Чипы для страницы «Рилсы»</returns>
    public static IReadOnlyList<StatusChipViewModel> Reels(string root)
    {
        using var document = Read(ReelsFile(root));
        var browser = document is not null && document.RootElement.TryGetProperty("cookiesFromBrowser", out var value) ? value.GetString() ?? string.Empty : string.Empty;
        var profile = string.Equals(browser, "firefox", StringComparison.OrdinalIgnoreCase)
            && Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mozilla", "Firefox", "Profiles"));

        return
        [
            Chip(Filled(document, null, "botToken"), "Токен бота", "задан", "не задан"),
            Chip(document is not null && document.RootElement.TryGetProperty("allowedUserId", out var owner) && owner.ValueKind == JsonValueKind.Number && owner.GetInt64() != 0, "Владелец бота", "задан", "не задан"),
            browser.Length == 0
                ? new StatusChipViewModel("Вход в Instagram", "не настроен", PanelToneEnum.Attention)
                : Chip(profile || !string.Equals(browser, "firefox", StringComparison.OrdinalIgnoreCase), "Вход в Instagram", browser, $"нет профиля {browser}"),
        ];
    }

    /// <summary>
    /// Состояние выгрузки занятий: вход в LMS, вход в MTS Link и разрешение писать в SingularityApp
    /// </summary>
    /// <param name="root">Корень репозитория</param>
    /// <returns>Чипы для страницы «Расшифровки»</returns>
    public static IReadOnlyList<StatusChipViewModel> Transcripts(string root)
    {
        using var document = Read(LmsFile(root));
        var state = Path.Combine(root, "data", "lms", "mts-state.json");
        var create = document is not null && document.RootElement.TryGetProperty("createTasks", out var flag) && flag.ValueKind == JsonValueKind.True;

        return
        [
            Chip(Filled(document, null, "login") && Filled(document, null, "password"), "Вход в LMS", "задан", "не заданы логин и пароль"),
            Chip(File.Exists(state), "Вход в MTS Link", File.Exists(state) ? "сохранён " + File.GetLastWriteTime(state).ToString("dd.MM") : string.Empty, "нет входа"),
            create ? new StatusChipViewModel("Запись в Singularity", "включена", PanelToneEnum.Running) : new StatusChipViewModel("Запись в Singularity", "выключена", PanelToneEnum.Attention),
        ];
    }

    /// <summary>
    /// Собирает чип: зелёный, когда всё в порядке, и жёлтый, когда нет
    /// </summary>
    /// <param name="ok">Всё задано</param>
    /// <param name="label">Что проверяется</param>
    /// <param name="good">Значение, когда всё в порядке</param>
    /// <param name="bad">Значение, когда нет</param>
    /// <returns>Показатель состояния</returns>
    private static StatusChipViewModel Chip(bool ok, string label, string good, string bad) => new(label, ok ? good : bad, ok ? PanelToneEnum.Running : PanelToneEnum.Attention);

    /// <summary>
    /// Проверяет, что значение в файле настроек заполнено и не осталось заглушкой в угловых скобках
    /// </summary>
    /// <param name="document">Разобранный файл или <see langword="null"/>, если его нет</param>
    /// <param name="section">Раздел файла или <see langword="null"/>, если ключ лежит в корне</param>
    /// <param name="key">Ключ без учёта регистра</param>
    /// <returns>
    /// <see langword="true"/>, если значение есть и не заглушка
    /// </returns>
    private static bool Filled(JsonDocument? document, string? section, string key)
    {
        if (document is null)
            return false;

        var scope = document.RootElement;
        if (section is not null && !TryGet(scope, section, out scope))
            return false;

        return TryGet(scope, key, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text && !text.StartsWith('<');
    }

    /// <summary>
    /// Ищет свойство без учёта регистра
    /// </summary>
    /// <param name="element">Объект JSON</param>
    /// <param name="name">Имя свойства</param>
    /// <param name="value">Найденное значение</param>
    /// <returns>
    /// <see langword="true"/>, если свойство нашлось
    /// </returns>
    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;

                    return true;
                }
            }
        }

        value = default;

        return false;
    }

    /// <summary>
    /// Читает файл настроек
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Разобранный файл или <see langword="null"/>, если его нет или он не читается</returns>
    private static JsonDocument? Read(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
