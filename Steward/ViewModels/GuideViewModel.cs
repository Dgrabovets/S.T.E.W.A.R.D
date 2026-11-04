using System.IO;
using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Строка списка в окне «Методические указания»: галочка «учитывать», название, откуда файл и кнопки «Открыть» и «Убрать»
/// </summary>
public sealed class GuideViewModel : Bindable
{
    /// <summary>
    /// Материал учитывается при подготовке заданий
    /// </summary>
    private bool _isEnabled;

    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="entry">Запись материала</param>
    /// <param name="folder">Папка материалов приложения: по ней видно, скопирован ли файл к себе</param>
    /// <param name="open">Открывает файл или папку</param>
    /// <param name="remove">Убирает материал из списка</param>
    /// <param name="enabledChanged">Вызывается, когда галочку поставили или сняли</param>
    public GuideViewModel(GuideEntry entry, string folder, Action<string> open, Action<GuideViewModel> remove, Action<string, bool>? enabledChanged = null)
    {
        Id = entry.Id;
        Title = entry.Title;
        Note = entry.Note;
        Kind = entry.Kind;
        _isEnabled = entry.Enabled;
        var copied = entry.Path.Length > 0 && entry.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        Meta = entry.Path.Length == 0 ? entry.Url : copied ? (entry.Url.Length > 0 ? "Скачано по ссылке в папку приложения" : "Скопировано в папку приложения") : (Directory.Exists(entry.Path) ? "Папка: " : "Файл: ") + entry.Path;
        OpenCommand = new RelayCommand(() => open(entry.Path.Length > 0 ? entry.Path : entry.Url));
        RemoveCommand = new RelayCommand(() => remove(this));
        EnabledChanged = enabledChanged;
        Mark = !entry.Id.StartsWith("sample:", StringComparison.Ordinal) ? string.Empty
            : "Сдано · " + entry.Subject + (entry.Manual ? " · файл указан вручную" : string.Empty);
    }

    /// <summary>
    /// Вызывается, когда галочку поставили или сняли
    /// </summary>
    private Action<string, bool>? EnabledChanged { get; }

    /// <summary>
    /// Ключ записи
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Указания или образец
    /// </summary>
    public GuideKindEnum Kind { get; }

    /// <summary>
    /// Название: имя файла или папки
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Откуда материал: скопирован, файл или папка по пути
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Пояснение к записи
    /// </summary>
    public string Note { get; }

    /// <summary>
    /// У записи есть пояснение
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Пометка образца из сданной работы: «Сдано · предмет», а если файл сданной работы указал владелец, ещё «файл указан вручную». У прочих записей пусто
    /// </summary>
    public string Mark { get; }

    /// <summary>
    /// У записи есть пометка образца
    /// </summary>
    public bool HasMark => Mark.Length > 0;

    /// <summary>
    /// Материал учитывается при подготовке заданий
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
                EnabledChanged?.Invoke(Id, value);
        }
    }

    /// <summary>
    /// Открывает материал
    /// </summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Убирает материал из списка
    /// </summary>
    public RelayCommand RemoveCommand { get; }
}
