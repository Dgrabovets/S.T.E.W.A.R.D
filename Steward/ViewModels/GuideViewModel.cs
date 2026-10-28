using System.IO;
using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Строка списка методических указаний или образцов предмета: название, откуда файл и кнопки «Открыть» и «Убрать»
/// </summary>
public sealed class GuideViewModel
{
    /// <summary>
    /// Создаёт строку
    /// </summary>
    /// <param name="entry">Запись материала</param>
    /// <param name="folder">Папка материалов приложения: по ней видно, скопирован ли файл к себе</param>
    /// <param name="open">Открывает файл или папку</param>
    /// <param name="remove">Убирает материал из списка</param>
    public GuideViewModel(GuideEntry entry, string folder, Action<string> open, Action<GuideViewModel> remove)
    {
        Id = entry.Id;
        Title = entry.Title;
        Note = entry.Note;
        var copied = entry.Path.Length > 0 && entry.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
        Meta = entry.Path.Length == 0 ? entry.Url : copied ? (entry.Url.Length > 0 ? "Скачано по ссылке в папку приложения" : "Скопировано в папку приложения") : (Directory.Exists(entry.Path) ? "Папка: " : "Файл: ") + entry.Path;
        OpenCommand = new RelayCommand(() => open(entry.Path.Length > 0 ? entry.Path : entry.Url));
        RemoveCommand = new RelayCommand(() => remove(this));
    }

    /// <summary>
    /// Ключ записи
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Название: имя файла или папки
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Откуда материал: скопирован, файл или папка по пути, ссылка
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Пояснение, например почему ссылку не удалось скачать
    /// </summary>
    public string Note { get; }

    /// <summary>
    /// У записи есть пояснение
    /// </summary>
    public bool HasNote => Note.Length > 0;

    /// <summary>
    /// Открывает материал
    /// </summary>
    public RelayCommand OpenCommand { get; }

    /// <summary>
    /// Убирает материал из списка
    /// </summary>
    public RelayCommand RemoveCommand { get; }
}
