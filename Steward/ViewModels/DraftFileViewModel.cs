using System.IO;
using System.Text.RegularExpressions;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Готовый файл с домашним заданием: на карточке занятия ему соответствует кнопка «Посмотреть задание», она открывает файл в нужной программе
/// </summary>
public sealed partial class DraftFileViewModel
{
    /// <summary>
    /// Создаёт кнопку
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="open">Открывает файл в программе для его формата</param>
    /// <param name="single">У занятия один готовый файл: тогда на кнопке нет номера задания</param>
    public DraftFileViewModel(string path, Action<string> open, bool single)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path);
        var number = Number().Match(Name);
        ButtonTitle = single || !number.Success ? "Посмотреть задание" : $"Посмотреть задание {number.Groups[1].Value}";
        OpenCommand = new RelayCommand(() => open(path));
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
    /// Подпись кнопки
    /// </summary>
    public string ButtonTitle { get; }

    /// <summary>
    /// Открывает файл
    /// </summary>
    public RelayCommand OpenCommand { get; }

    [GeneratedRegex(@"^Задание (\d+)")]
    private static partial Regex Number();
}
