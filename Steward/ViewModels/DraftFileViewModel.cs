using System.IO;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Готовый файл с домашним заданием: показывается в списке занятия и открывается в предпросмотре
/// </summary>
public sealed class DraftFileViewModel : Bindable
{
    /// <summary>
    /// Файл открыт в предпросмотре
    /// </summary>
    private bool _isOpen;

    /// <summary>
    /// Создаёт строку файла
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="open">Действие по нажатию: открыть предпросмотр</param>
    public DraftFileViewModel(string path, Action<DraftFileViewModel> open)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        Kind = System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".docx" => "Word",
            ".pptx" => "Презентация",
            ".md" or ".markdown" => "Markdown",
            ".txt" => "Текст",
            ".pdf" => "PDF",
            "" => "Файл",
            var other => "Код " + other,
        };
        Meta = $"{Kind}  ·  {File.GetLastWriteTime(path).ToString("dd.MM HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
        OpenCommand = new RelayCommand(() => open(this));
    }

    /// <summary>
    /// Путь к файлу
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Имя файла
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Что за формат
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Формат и время создания
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Файл открыт в предпросмотре
    /// </summary>
    public bool IsOpen
    {
        get => _isOpen;
        set => Set(ref _isOpen, value);
    }

    /// <summary>
    /// Открывает предпросмотр
    /// </summary>
    public RelayCommand OpenCommand { get; }
}
