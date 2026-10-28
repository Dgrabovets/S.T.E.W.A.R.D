using System.Globalization;
using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Домашнее задание, которое нашли в расшифровке занятия: название, срок, требования и кнопки «В архив» и «Добавить в Singularity»
/// </summary>
public sealed class LessonItemViewModel
{
    /// <summary>
    /// Самая длинная часть требований на карточке в знаках
    /// </summary>
    private const int REQUIREMENTS_LIMIT = 220;

    /// <summary>
    /// Создаёт строку задания
    /// </summary>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="item">Задание</param>
    /// <param name="inSingularity">Задача по заданию уже заведена в SingularityApp</param>
    /// <param name="archive">Убирает задание в архив</param>
    /// <param name="addToSingularity">Заводит задачу в SingularityApp</param>
    public LessonItemViewModel(int number, HomeworkItem item, bool inSingularity, Action<LessonItemViewModel> archive, Action<LessonItemViewModel> addToSingularity)
    {
        Number = number;
        Title = item.Title;
        IsDone = item.Done;
        InSingularity = inSingularity;
        Requirements = item.Requirements.Length > REQUIREMENTS_LIMIT ? item.Requirements[..REQUIREMENTS_LIMIT].TrimEnd() + "…" : item.Requirements;
        Meta = string.Join("  ·  ", new[]
        {
            item.Due is { } due ? $"Срок: {due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : "Срок не назван",
            item.Done ? "уже сдано" : string.Empty,
            inSingularity ? "задача в Singularity" : string.Empty,
        }.Where(part => part.Length > 0));
        ArchiveCommand = new RelayCommand(() => archive(this));
        AddToSingularityCommand = new RelayCommand(() => addToSingularity(this), () => !InSingularity);
    }

    /// <summary>
    /// Номер задания в занятии, с единицы
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Что нужно сделать
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Срок и пометки
    /// </summary>
    public string Meta { get; }

    /// <summary>
    /// Требования к работе, начало
    /// </summary>
    public string Requirements { get; }

    /// <summary>
    /// У задания есть требования
    /// </summary>
    public bool HasRequirements => Requirements.Length > 0;

    /// <summary>
    /// По данным LMS задание уже сдано
    /// </summary>
    public bool IsDone { get; }

    /// <summary>
    /// Задача по заданию уже заведена в SingularityApp
    /// </summary>
    public bool InSingularity { get; }

    /// <summary>
    /// Убирает задание в архив
    /// </summary>
    public RelayCommand ArchiveCommand { get; }

    /// <summary>
    /// Заводит задачу в SingularityApp
    /// </summary>
    public RelayCommand AddToSingularityCommand { get; }
}
