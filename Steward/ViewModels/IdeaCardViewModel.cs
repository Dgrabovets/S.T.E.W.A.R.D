using System.Globalization;
using Steward.Ideas.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Карточка идеи в разделе «Идеи»: название, откуда она, начало расшифровки и кнопки решения
/// </summary>
public sealed class IdeaCardViewModel
{
    /// <summary>
    /// Длина превью расшифровки в символах
    /// </summary>
    private const int PREVIEW_LENGTH = 320;

    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="idea">Идея</param>
    /// <param name="approve">Вызывается по кнопке «Делаем»</param>
    /// <param name="reject">Вызывается по кнопке «Отклонить»</param>
    /// <param name="open">Вызывается по ссылке на рилс: параметр — адрес</param>
    public IdeaCardViewModel(Idea idea, Action<Idea> approve, Action<Idea> reject, Action<string> open)
    {
        Title = idea.Title;
        Meta = string.Join(" · ", new[] { idea.Author, idea.CreatedAt.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture) }.Where(part => part.Length > 0));
        Preview = Shorten(idea.Transcript.Length > 0 ? idea.Transcript : idea.Caption);
        ApproveCommand = new RelayCommand(() => approve(idea));
        RejectCommand = new RelayCommand(() => reject(idea));
        OpenSourceCommand = new RelayCommand(() => open(idea.SourceUrl));
    }

    /// <summary>
    /// Название идеи
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Автор рилса и время получения
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Начало расшифровки или, если речи нет, подписи
    /// </summary>
    public string Preview { get; }

    /// <summary>
    /// Есть что показать под названием
    /// </summary>
    public bool HasPreview => Preview.Length > 0;

    /// <summary>
    /// Решено делать: открывает Claude с просьбой разобрать идею
    /// </summary>
    public RelayCommand ApproveCommand { get; }

    /// <summary>
    /// Решено не делать
    /// </summary>
    public RelayCommand RejectCommand { get; }

    /// <summary>
    /// Открывает рилс в браузере
    /// </summary>
    public RelayCommand OpenSourceCommand { get; }

    /// <summary>
    /// Сжимает пробелы и переводы строк и обрезает текст по слову
    /// </summary>
    /// <param name="text">Расшифровка или подпись</param>
    /// <returns>Одна строка не длиннее лимита, с многоточием, если текст был длиннее</returns>
    private static string Shorten(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (line.Length <= PREVIEW_LENGTH)
            return line;

        var cut = line[..PREVIEW_LENGTH];
        var space = cut.LastIndexOf(' ');

        return (space > PREVIEW_LENGTH / 2 ? cut[..space] : cut) + "…";
    }
}