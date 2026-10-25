using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Mail.Interfaces;

namespace Steward.Homework.Services;

/// <summary>
/// Создаёт в SingularityApp задачи по заданиям, которые агент нашёл в расшифровках. Занятия с форматом «Не готовить» пропускаются, готовые задачи не дублируются
/// </summary>
/// <param name="store">Папка домашних заданий</param>
/// <param name="singularity">Клиент SingularityApp</param>
/// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
/// <param name="timeProvider">Часы: часовой пояс начала дня</param>
/// <param name="log">Принимает строки лога</param>
public sealed class HomeworkTaskCreator(HomeworkStore store, ISingularityClient singularity, string? projectId, TimeProvider timeProvider, Action<string> log)
{
    /// <summary>
    /// Читает файлы заданий и создаёт недостающие задачи
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько задач создано и сколько заданий уже были в SingularityApp</returns>
    public async Task<(int Created, int Existing)> RunAsync(CancellationToken cancellationToken)
    {
        var ready = store.LoadLessons()
            .Where(lesson => store.FormatFor(lesson.Id) != HomeworkFormatEnum.Skip && store.ReadItems(lesson.Id) is not null)
            .ToList();
        if (ready.Count == 0)
            return (0, 0);

        var offset = timeProvider.GetLocalNow().Offset;
        var known = await singularity.ListExternalIdsAsync(new DateTimeOffset(ready.Min(lesson => lesson.Date).AddDays(-1).ToDateTime(TimeOnly.MinValue), offset), cancellationToken);

        var created = 0;
        var existing = 0;
        foreach (var lesson in ready)
        {
            var items = store.ReadItems(lesson.Id)!;
            for (var index = 0; index < items.Count; index++)
            {
                if (known.Contains(HomeworkTaskBuilder.ExternalId(lesson, index + 1)))
                {
                    existing++;

                    continue;
                }

                await singularity.CreateTaskAsync(HomeworkTaskBuilder.Build(lesson, items[index], index + 1, store.FormatFor(lesson.Id), projectId, offset), cancellationToken);
                log($"ДЗ: {items[index].Title} ({lesson.Title})");
                created++;
            }
        }

        return (created, existing);
    }
}