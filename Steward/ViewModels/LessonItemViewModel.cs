using System.Globalization;
using Steward.Homework.Models;
using Steward.Mvvm;

namespace Steward.ViewModels;

/// <summary>
/// Домашнее задание, которое нашли в расшифровке занятия: название, срок, требования и кнопки «В архив», «Добавить в Singularity», «Открыть в LMS», «Отметить сданным» и «Скопировать условие»
/// </summary>
public sealed class LessonItemViewModel
{
    /// <summary>
    /// Самая длинная часть требований на карточке в знаках
    /// </summary>
    private const int REQUIREMENTS_LIMIT = 220;

    /// <summary>
    /// Сколько дней после правки условия в LMS на карточке остаётся пометка
    /// </summary>
    private const int CHANGED_DAYS = 14;

    /// <summary>
    /// Создаёт строку задания
    /// </summary>
    /// <param name="number">Номер задания в занятии, с единицы</param>
    /// <param name="item">Задание</param>
    /// <param name="inSingularity">Задача по заданию уже заведена в SingularityApp</param>
    /// <param name="archive">Убирает задание в архив</param>
    /// <param name="addToSingularity">Заводит задачу в SingularityApp</param>
    /// <param name="openInLms">Открывает страницу задания в LMS</param>
    /// <param name="toggleSubmitted">Отмечает задание сданным или снимает отметку</param>
    /// <param name="copyCondition">Копирует условие задания в буфер обмена</param>
    public LessonItemViewModel(int number, HomeworkItem item, bool inSingularity, Action<LessonItemViewModel> archive, Action<LessonItemViewModel> addToSingularity, Action<LessonItemViewModel>? openInLms = null, Action<LessonItemViewModel>? toggleSubmitted = null, Action<LessonItemViewModel>? copyCondition = null)
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
            item.Changed is { } changed && changed >= DateOnly.FromDateTime(DateTime.Now).AddDays(-CHANGED_DAYS) ? $"условие в LMS изменено {changed.ToString("dd.MM", CultureInfo.InvariantCulture)}" : string.Empty,
        }.Where(part => part.Length > 0));
        ArchiveCommand = new RelayCommand(() => archive(this));
        AddToSingularityCommand = new RelayCommand(() => addToSingularity(this), () => !InSingularity);
        Submitted = item.Submitted;
        OpenInLmsCommand = new RelayCommand(() => openInLms?.Invoke(this), () => openInLms is not null);
        ToggleSubmittedCommand = new RelayCommand(() => toggleSubmitted?.Invoke(this), () => toggleSubmitted is not null && (!IsDone || Submitted));
        CopyConditionCommand = new RelayCommand(() => copyCondition?.Invoke(this), () => copyCondition is not null);
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
    /// Задание отмечено сданным вручную: отметку можно снять
    /// </summary>
    public bool Submitted { get; }

    /// <summary>
    /// Подпись кнопки сдачи: поставить или снять отметку
    /// </summary>
    public string SubmitLabel => Submitted ? "Вернуть в работу" : "Отметить сданным";

    /// <summary>
    /// Кнопка сдачи нужна: задание ещё не сдано или отмечено сданным вручную. Сдачу по данным LMS отменить нельзя
    /// </summary>
    public bool CanToggleSubmitted => !IsDone || Submitted;

    /// <summary>
    /// Убирает задание в архив
    /// </summary>
    public RelayCommand ArchiveCommand { get; }

    /// <summary>
    /// Открывает страницу задания в LMS
    /// </summary>
    public RelayCommand OpenInLmsCommand { get; }

    /// <summary>
    /// Отмечает задание сданным или снимает отметку
    /// </summary>
    public RelayCommand ToggleSubmittedCommand { get; }

    /// <summary>
    /// Копирует условие задания в буфер обмена
    /// </summary>
    public RelayCommand CopyConditionCommand { get; }

    /// <summary>
    /// Заводит задачу в SingularityApp
    /// </summary>
    public RelayCommand AddToSingularityCommand { get; }
}
