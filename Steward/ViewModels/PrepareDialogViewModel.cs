using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Окно «Подготовить задание»: формат работы, дополнительное указание и список методических указаний и образцов, которые нужно учесть
/// </summary>
public sealed class PrepareDialogViewModel : Bindable
{
    /// <summary>
    /// Выбранный формат
    /// </summary>
    private HomeworkFormatEnum _format;

    /// <summary>
    /// Дополнительное указание
    /// </summary>
    private string _extra = string.Empty;

    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="heading">Занятие, для которого готовятся задания</param>
    /// <param name="format">Формат, выбранный сейчас</param>
    /// <param name="materials">Материалы предмета</param>
    /// <param name="folder">Папка материалов приложения</param>
    public PrepareDialogViewModel(string heading, HomeworkFormatEnum format, IReadOnlyList<GuideEntry> materials, string folder)
    {
        Heading = heading;
        _format = format == HomeworkFormatEnum.Skip ? HomeworkFormats.DEFAULT : format;
        Formats = [.. HomeworkFormats.All.Where(item => item != HomeworkFormatEnum.Skip).Select(item => new FormatChoiceViewModel(item, this))];
        GuideChoiceViewModel Choice(GuideEntry entry) => new(entry, folder);
        Guides = [.. materials.Where(entry => entry.Kind == GuideKindEnum.Guide).Select(Choice)];
        Examples = [.. materials.Where(entry => entry.Kind == GuideKindEnum.Example).Select(Choice)];
    }

    /// <summary>
    /// Занятие, для которого готовятся задания
    /// </summary>
    public string Heading { get; }

    /// <summary>
    /// Форматы на выбор
    /// </summary>
    public IReadOnlyList<FormatChoiceViewModel> Formats { get; }

    /// <summary>
    /// Методические указания предмета с галочками
    /// </summary>
    public IReadOnlyList<GuideChoiceViewModel> Guides { get; }

    /// <summary>
    /// Образцы предмета с галочками
    /// </summary>
    public IReadOnlyList<GuideChoiceViewModel> Examples { get; }

    /// <summary>
    /// Есть методические указания
    /// </summary>
    public bool HasGuides => Guides.Count > 0;

    /// <summary>
    /// Есть образцы
    /// </summary>
    public bool HasExamples => Examples.Count > 0;

    /// <summary>
    /// Материалов нет: блок выбора не нужен
    /// </summary>
    public bool NoMaterials => !HasGuides && !HasExamples;

    /// <summary>
    /// Выбранный формат
    /// </summary>
    public HomeworkFormatEnum Format
    {
        get => _format;
        set
        {
            if (!Set(ref _format, value))
                return;

            foreach (var choice in Formats)
                choice.Refresh();
        }
    }

    /// <summary>
    /// Дополнительное указание, которое увидит Claude
    /// </summary>
    public string Extra
    {
        get => _extra;
        set => Set(ref _extra, value);
    }

    /// <summary>
    /// Собирает запрос на подготовку из выбранного
    /// </summary>
    /// <returns>Запрос: формат, указание и ключи отмеченных материалов</returns>
    public PrepareRequest Request() => new(Format, Extra.Trim(), Guides.Concat(Examples).Where(choice => choice.IsChecked).Select(choice => choice.Id).ToHashSet(StringComparer.Ordinal));
}

/// <summary>
/// Пункт выбора формата в окне «Подготовить задание»
/// </summary>
public sealed class FormatChoiceViewModel : Bindable
{
    /// <summary>
    /// Окно, где идёт выбор
    /// </summary>
    private readonly PrepareDialogViewModel _owner;

    /// <summary>
    /// Создаёт пункт
    /// </summary>
    /// <param name="format">Формат</param>
    /// <param name="owner">Окно</param>
    public FormatChoiceViewModel(HomeworkFormatEnum format, PrepareDialogViewModel owner)
    {
        Format = format;
        _owner = owner;
        SelectCommand = new RelayCommand(() => owner.Format = format);
    }

    /// <summary>
    /// Формат
    /// </summary>
    public HomeworkFormatEnum Format { get; }

    /// <summary>
    /// Название для кнопки
    /// </summary>
    public string Title => HomeworkFormats.Title(Format);

    /// <summary>
    /// Формат выбран
    /// </summary>
    public bool IsSelected => _owner.Format == Format;

    /// <summary>
    /// Выбирает формат
    /// </summary>
    public RelayCommand SelectCommand { get; }

    /// <summary>
    /// Сообщает окну, что выбор поменялся
    /// </summary>
    public void Refresh() => Raise(nameof(IsSelected));
}

/// <summary>
/// Материал с галочкой в окне «Подготовить задание»
/// </summary>
public sealed class GuideChoiceViewModel : Bindable
{
    /// <summary>
    /// Материал учитывается
    /// </summary>
    private bool _isChecked;

    /// <summary>
    /// Создаёт пункт
    /// </summary>
    /// <param name="entry">Материал</param>
    /// <param name="folder">Папка материалов приложения</param>
    public GuideChoiceViewModel(GuideEntry entry, string folder)
    {
        Id = entry.Id;
        Title = entry.Title;
        Meta = entry.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) ? "Скопировано в папку приложения" : entry.Path;
        _isChecked = entry.Enabled;
    }

    /// <summary>
    /// Ключ материала
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Название
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Откуда материал
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Материал учитывается
    /// </summary>
    public bool IsChecked
    {
        get => _isChecked;
        set => Set(ref _isChecked, value);
    }
}
