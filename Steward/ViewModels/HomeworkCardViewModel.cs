using System.Globalization;
using Steward.Homework.Models;
using Steward.Models;

namespace Steward.ViewModels;

/// <summary>
/// Карточка найденного задания на странице «Домашние задания»
/// </summary>
public sealed class HomeworkCardViewModel
{
    /// <summary>
    /// Длина превью требований в символах
    /// </summary>
    private const int PREVIEW_LENGTH = 280;

    /// <summary>
    /// Создаёт карточку
    /// </summary>
    /// <param name="row">Задание с занятием и отметкой о задаче</param>
    public HomeworkCardViewModel(HomeworkRow row)
    {
        Title = row.Item.Title;
        Lesson = $"{row.Lesson.Title}, {row.Lesson.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        Due = row.Item.Due is { } due ? $"Срок: {due.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}" : "Срок не назван";
        Format = HomeworkFormats.Title(row.Format);
        Requirements = row.Item.Requirements.Length > PREVIEW_LENGTH ? row.Item.Requirements[..PREVIEW_LENGTH].TrimEnd() + "…" : row.Item.Requirements;
        (Status, Tone, Kind) = row switch
        {
            { Created: true } => ("Задача создана", PanelToneEnum.Running, ActivityKindEnum.Calm),
            { Format: HomeworkFormatEnum.Skip } => ("Не готовить", PanelToneEnum.Idle, ActivityKindEnum.Dormant),
            _ => ("Ждёт создания задачи", PanelToneEnum.Attention, ActivityKindEnum.Attention),
        };
    }

    /// <summary>
    /// Что нужно сделать
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Занятие и его день
    /// </summary>
    public string Lesson { get; }

    /// <summary>
    /// Срок сдачи или пометка, что его не назвали
    /// </summary>
    public string Due { get; }

    /// <summary>
    /// Формат работы
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Начало требований к работе
    /// </summary>
    public string Requirements { get; }

    /// <summary>
    /// Есть ли требования, которые можно показать
    /// </summary>
    public bool HasRequirements => Requirements.Length > 0;

    /// <summary>
    /// Что с задачей в SingularityApp
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Вид сферы на плашке: «ждёт» у задачи, которую ещё не создали
    /// </summary>
    public ActivityKindEnum Kind { get; }

    /// <summary>
    /// Цвет плашки статуса
    /// </summary>
    public PanelToneEnum Tone { get; }
}