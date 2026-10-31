using Steward.Homework.Models;

namespace Steward.ViewModels;

/// <summary>
/// Окно «Уточнить для предмета»: общие методические указания и образцы с галочкой «относится к предмету»
/// </summary>
public sealed class SubjectGuidesDialogViewModel
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="subject">Название предмета</param>
    /// <param name="materials">Общие материалы: указания и образцы. Образцы из сданных работ сюда не входят, они и так привязаны к своему предмету</param>
    /// <param name="excluded">Ключи материалов, которые к предмету уже не относятся</param>
    /// <param name="folder">Папка материалов приложения: по ней видно, скопирован ли файл к себе</param>
    public SubjectGuidesDialogViewModel(string subject, IReadOnlyList<GuideEntry> materials, IReadOnlyCollection<string> excluded, string folder)
    {
        Subject = subject;
        GuideChoiceViewModel Choice(GuideEntry entry) => new(entry, folder) { IsChecked = !excluded.Contains(entry.Id) };
        Guides = [.. materials.Where(entry => entry.Kind == GuideKindEnum.Guide).Select(Choice)];
        Examples = [.. materials.Where(entry => entry.Kind == GuideKindEnum.Example).Select(Choice)];
    }

    /// <summary>
    /// Название предмета
    /// </summary>
    public string Subject { get; }

    /// <summary>
    /// Методические указания
    /// </summary>
    public IReadOnlyList<GuideChoiceViewModel> Guides { get; }

    /// <summary>
    /// Образцы
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
    /// Общих материалов нет совсем
    /// </summary>
    public bool NoMaterials => !HasGuides && !HasExamples;

    /// <summary>
    /// Ключи материалов без галочки: они к предмету не относятся
    /// </summary>
    /// <returns>Ключи исключённых материалов</returns>
    public IReadOnlyList<string> Excluded() => [.. Guides.Concat(Examples).Where(choice => !choice.IsChecked).Select(choice => choice.Id)];
}
