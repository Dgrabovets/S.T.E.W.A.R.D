using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Страница настроек процесса: поля из его JSON-файла. Остальные ключи файла при записи остаются как были
/// </summary>
public sealed class JsonSettingsViewModel : Bindable
{
    /// <summary>
    /// Как записывать файл обратно
    /// </summary>
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// Путь к файлу настроек
    /// </summary>
    private readonly string _file;

    /// <summary>
    /// Что показать под кнопками
    /// </summary>
    private string _message = string.Empty;

    /// <summary>
    /// Создаёт страницу настроек
    /// </summary>
    /// <param name="file">Путь к файлу настроек</param>
    /// <param name="intro">Пояснение над полями</param>
    /// <param name="sections">Разделы страницы с полями</param>
    /// <param name="openFile">Действие кнопки «Открыть файл настроек»</param>
    public JsonSettingsViewModel(string file, string intro, IReadOnlyList<SettingsSectionViewModel> sections, Action openFile)
    {
        _file = file;
        Intro = intro;
        Sections = sections;
        Fields = [.. sections.SelectMany(section => section.Fields)];
        OpenFileCommand = new RelayCommand(openFile);
        SaveCommand = new RelayCommand(Save);
        Load();
    }

    /// <summary>
    /// Файл записан: нужно обновить плашки состояния
    /// </summary>
    public event Action? Saved;

    /// <summary>
    /// Пояснение над полями
    /// </summary>
    public string Intro { get; }

    /// <summary>
    /// Разделы страницы с полями
    /// </summary>
    public IReadOnlyList<SettingsSectionViewModel> Sections { get; }

    /// <summary>
    /// Все поля страницы одним списком
    /// </summary>
    private IReadOnlyList<SettingFieldViewModel> Fields { get; }

    /// <summary>
    /// Над разделами есть пояснение
    /// </summary>
    public bool HasIntro => Intro.Length > 0;

    /// <summary>
    /// Открывает файл настроек в программе по умолчанию
    /// </summary>
    public RelayCommand OpenFileCommand { get; }

    /// <summary>
    /// Записывает значения полей в файл
    /// </summary>
    public RelayCommand SaveCommand { get; }

    /// <summary>
    /// Что показать под кнопками
    /// </summary>
    public string Message
    {
        get => _message;
        private set
        {
            if (Set(ref _message, value))
                Raise(nameof(HasMessage));
        }
    }

    /// <summary>
    /// Под кнопками есть сообщение
    /// </summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Перечитывает значения из файла: заглушки в угловых скобках показываются пустыми
    /// </summary>
    public void Load()
    {
        JsonNode? root = null;
        try
        {
            if (File.Exists(_file))
                root = JsonNode.Parse(File.ReadAllText(_file), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            Message = "Файл настроек не читается: " + exception.Message;
        }

        foreach (var field in Fields)
        {
            var text = Find(root, field.Path)?.ToString() ?? string.Empty;
            field.Value = text.StartsWith('<') ? string.Empty : text;
        }
    }

    /// <summary>
    /// Записывает значения в файл, не трогая остальные ключи
    /// </summary>
    private void Save()
    {
        try
        {
            var root = File.Exists(_file) ? JsonNode.Parse(File.ReadAllText(_file), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip }) as JsonObject ?? [] : [];

            foreach (var field in Fields)
            {
                var value = field.Value.Trim();
                if (value.Length == 0 && Find(root, field.Path) is null)
                    continue;

                if (field.IsNumber && !long.TryParse(value, out _))
                {
                    Message = $"«{field.Title}» — целое число";

                    return;
                }

                Put(root, field.Path, field.IsNumber ? JsonValue.Create(long.Parse(value)) : JsonValue.Create(value));
            }

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, root.ToJsonString(WriteOptions), new UTF8Encoding(false));
            Message = "Настройки записаны. Новые значения подхватятся при следующем запуске задачи";
            Saved?.Invoke();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            Message = "Не удалось записать файл: " + exception.Message;
        }
    }

    /// <summary>
    /// Ищет значение по пути без учёта регистра ключей
    /// </summary>
    /// <param name="root">Корень файла</param>
    /// <param name="path">Ключи через точку</param>
    /// <returns>Найденный узел или <see langword="null"/></returns>
    private static JsonNode? Find(JsonNode? root, string path)
    {
        var node = root;
        foreach (var key in path.Split('.'))
        {
            node = node is JsonObject obj ? obj.FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)).Value : null;
        }

        return node;
    }

    /// <summary>
    /// Кладёт значение по пути, создавая недостающие разделы и сохраняя написание существующих ключей
    /// </summary>
    /// <param name="root">Корень файла</param>
    /// <param name="path">Ключи через точку</param>
    /// <param name="value">Значение</param>
    private static void Put(JsonObject root, string path, JsonNode? value)
    {
        var keys = path.Split('.');
        var current = root;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            var name = current.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, keys[i], StringComparison.OrdinalIgnoreCase)) ?? keys[i];
            if (current[name] is not JsonObject next)
            {
                next = [];
                current[name] = next;
            }

            current = next;
        }

        var last = current.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, keys[^1], StringComparison.OrdinalIgnoreCase)) ?? keys[^1];
        current[last] = value;
    }
}
