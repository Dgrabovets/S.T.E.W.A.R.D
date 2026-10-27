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
    /// Самая длинная метка абзаца, например «Минусы и риски»
    /// </summary>
    private const int LABEL_LIMIT = 24;

    /// <summary>
    /// Длина превью расшифровки в символах
    /// </summary>
    private const int PREVIEW_LENGTH = 320;

    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="idea">Идея</param>
    /// <param name="approve">Вызывается по кнопке «Делаем»</param>
    /// <param name="reject">Вызывается по кнопке «Удалить»</param>
    /// <param name="open">Вызывается по ссылке на рилс: параметр — адрес</param>
    public IdeaCardViewModel(Idea idea, Action<Idea> approve, Action<Idea> reject, Action<string> open)
    {
        Title = idea.Title;
        Meta = string.Join(" · ", new[] { idea.Author, idea.CreatedAt.ToLocalTime().ToString("dd.MM HH:mm", CultureInfo.InvariantCulture), idea.State == IdeaStateEnum.Approved ? "в работе" : string.Empty }.Where(part => part.Length > 0));
        Sections = Split(idea.Summary);
        Preview = Sections.Count > 0 ? string.Empty : Shorten(idea.Transcript.Length > 0 ? idea.Transcript : idea.Caption);
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
    /// Краткий разбор идеи, а если его нет, начало расшифровки или подписи
    /// </summary>
    public string Preview { get; }

    /// <summary>
    /// Разбор идеи по абзацам: метка («Суть», «Плюсы») и текст. Пусто, если разбора нет
    /// </summary>
    public IReadOnlyList<IdeaSection> Sections { get; }

    /// <summary>
    /// Есть разбор по абзацам
    /// </summary>
    public bool HasSections => Sections.Count > 0;

    /// <summary>
    /// Есть что показать под названием
    /// </summary>
    public bool HasPreview => Preview.Length > 0;

    /// <summary>
    /// Делает первую букву заглавной, чтобы абзац после метки читался как предложение
    /// </summary>
    /// <param name="text">Текст абзаца</param>
    /// <returns>Тот же текст с заглавной первой буквой</returns>
    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

    /// <summary>
    /// Делит разбор на абзацы: каждая строка вида «Метка: текст» становится отдельным абзацем, строка без метки остаётся абзацем без метки
    /// </summary>
    /// <param name="summary">Разбор из файла идеи</param>
    /// <returns>Абзацы в порядке записи</returns>
    private static IReadOnlyList<IdeaSection> Split(string summary) =>
        [.. summary.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line =>
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);

            return colon is > 0 and <= LABEL_LIMIT && line[..colon].Split(' ').Length <= 3
                ? new IdeaSection(line[..colon], Capitalize(line[(colon + 1)..].Trim()))
                : new IdeaSection(string.Empty, line);
        })];

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