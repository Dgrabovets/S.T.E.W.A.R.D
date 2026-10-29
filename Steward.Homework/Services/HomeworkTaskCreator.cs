using Steward.Homework.Models;
using Steward.Homework.Storage;
using Steward.Mail.Interfaces;

namespace Steward.Homework.Services;

/// <summary>
/// Создаёт в SingularityApp задачи по заданиям, которые агент нашёл в расшифровках. Занятия с форматом «Не готовить» пропускаются; задания, ждущие подтверждения владельца, сданные и убранные в архив задач не получают; готовые задачи не дублируются
/// </summary>
/// <param name="store">Папка домашних заданий</param>
/// <param name="singularity">Клиент SingularityApp</param>
/// <param name="projectId">Проект SingularityApp или <see langword="null"/></param>
/// <param name="timeProvider">Часы: часовой пояс начала дня</param>
/// <param name="log">Принимает строки лога</param>
/// <param name="dryRun">Только писать в лог, что было бы заведено: клиент не вызывается и отметки не ставятся</param>
public sealed class HomeworkTaskCreator(HomeworkStore store, ISingularityClient singularity, string? projectId, TimeProvider timeProvider, Action<string> log, bool dryRun = false)
{
    /// <summary>
    /// Читает файлы заданий и создаёт недостающие задачи. Сбой на одном задании пишется в лог и не останавливает остальные
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько задач создано и сколько заданий уже были в SingularityApp. При <c>dryRun</c> всегда ноль и ноль</returns>
    public async Task<(int Created, int Existing)> RunAsync(CancellationToken cancellationToken)
    {
        var ready = store.LoadLessons()
            .Where(lesson => store.FormatFor(lesson.Id) != HomeworkFormatEnum.Skip && store.ReadItems(lesson.Id) is not null)
            .ToList();
        if (ready.Count == 0)
            return (0, 0);

        var offset = timeProvider.GetLocalNow().Offset;
        var remembered = store.LoadCreated();
        IReadOnlySet<string> known = dryRun
            ? new HashSet<string>(StringComparer.Ordinal)
            : await singularity.ListExternalIdsAsync(new DateTimeOffset(ready.Min(lesson => lesson.Date).AddDays(-1).ToDateTime(TimeOnly.MinValue), offset), cancellationToken);

        var created = 0;
        var existing = 0;
        var handled = new List<string>();
        foreach (var lesson in ready)
        {
            var items = store.ReadItems(lesson.Id)!;
            foreach (var number in WantedNumbers(lesson, items))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var item = items[number - 1];
                var externalId = HomeworkTaskBuilder.ExternalId(lesson, number);
                if (known.Contains(externalId) || remembered.Contains(externalId))
                {
                    existing++;
                    handled.Add(externalId);

                    continue;
                }

                if (dryRun)
                {
                    log($"Завёл бы: {item.Title} ({lesson.Title})");

                    continue;
                }

                try
                {
                    await singularity.CreateTaskAsync(HomeworkTaskBuilder.Build(lesson, item, number, store.FormatFor(lesson.Id), projectId, offset), cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    log($"Не удалось завести задачу «{item.Title}» ({lesson.Title}): {exception.GetType().Name}: {exception.Message}");

                    continue;
                }

                handled.Add(externalId);
                store.MarkCreated([externalId]);
                log($"ДЗ: {item.Title} ({lesson.Title})");
                created++;
            }
        }

        if (!dryRun)
            store.MarkCreated(handled);

        return (created, existing);
    }

    /// <summary>
    /// Номера заданий занятия, по которым нужна задача: не сданные, не в архиве и не ждущие подтверждения владельца
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="items">Задания занятия</param>
    /// <returns>Номера с единицы по порядку</returns>
    private List<int> WantedNumbers(Lesson lesson, IReadOnlyList<HomeworkItem> items)
    {
        var submitted = store.SubmittedNumbers(lesson.Id);
        var archived = store.ArchivedNumbers(lesson.Id);
        var confirmed = store.ConfirmedNumbers(lesson.Id);
        var hasTranscript = store.HasTranscript(lesson.Id);
        var transcript = hasTranscript ? store.ReadTranscript(lesson.Id) : string.Empty;
        var numbers = new List<int>();

        for (var number = 1; number <= items.Count; number++)
        {
            var item = items[number - 1];
            if (item.Done || item.Submitted || submitted.Contains(number) || archived.Contains(number))
                continue;

            var assessment = ItemAssessor.Assess(item, EvidenceCheck.Verify(transcript, item.Evidence), hasTranscript, store.HasAssignmentCondition(item), confirmed.Contains(number));
            if (assessment.Readiness != ItemReadinessEnum.NeedsConfirmation)
                numbers.Add(number);
        }

        return numbers;
    }
}
