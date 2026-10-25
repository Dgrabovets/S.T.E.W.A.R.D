using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Tests;

/// <summary>
/// Проверяет путь от файла агента до задач SingularityApp: разбор, сборку задачи и дедупликацию
/// </summary>
public class HomeworkTasksTests
{
    /// <summary>
    /// Занятие 22 октября 2026
    /// </summary>
    private static readonly Lesson Sample = new("db-1022", "Базы данных: нормализация", new DateOnly(2026, 10, 22));

    /// <summary>
    /// Разбор берёт нормальные элементы, срок читает по формату, а мусор и элементы без названия пропускает
    /// </summary>
    [Fact]
    public void The_parser_keeps_valid_items_and_skips_the_rest()
    {
        var items = HomeworkItemParser.Parse("""
            ```json
            [
              {"title":"Нормализовать схему до 3НФ","due":"2026-10-29","requirements":"Диаграмма и пояснение","link":"https://lms.example/1"},
              {"title":"Без срока","due":"через неделю"},
              {"requirements":"нет названия"},
              "строка",
              {"title":"  ","due":null}
            ]
            ```
            """);

        Assert.Equal(["Нормализовать схему до 3НФ", "Без срока"], items.Select(item => item.Title).ToArray());
        Assert.Equal(new DateOnly(2026, 10, 29), items[0].Due);
        Assert.Null(items[1].Due);
        Assert.Equal("https://lms.example/1", items[0].Link);
    }

    /// <summary>
    /// Не JSON и не массив дают пустой список, а не исключение
    /// </summary>
    [Theory]
    [InlineData("{ не json")]
    [InlineData("{\"title\":\"а\"}")]
    [InlineData("")]
    public void The_parser_returns_nothing_for_unusable_text(string text) => Assert.Empty(HomeworkItemParser.Parse(text));

    /// <summary>
    /// Задача начинается в день срока, держит внешний ключ и показывает формат и требования
    /// </summary>
    [Fact]
    public void The_task_starts_on_the_due_day_with_a_stable_key()
    {
        var task = HomeworkTaskBuilder.Build(Sample, new HomeworkItem("Схема до 3НФ", new DateOnly(2026, 10, 29), "Диаграмма", "https://lms.example/1"), 2, HomeworkFormatEnum.Presentation, "proj", TimeSpan.FromHours(3));

        Assert.Equal("ДЗ: Схема до 3НФ", task.Title);
        Assert.Equal("2026-10-29T00:00:00+03:00", task.Start);
        Assert.Equal("hw:db-1022:2", task.ExternalId);
        Assert.Equal("proj", task.ProjectId);
        Assert.Contains("Презентация", task.Note, StringComparison.Ordinal);
        Assert.Contains("Диаграмма", task.Note, StringComparison.Ordinal);
        Assert.Contains("https://lms.example/1", task.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Без срока задача начинается в день занятия
    /// </summary>
    [Fact]
    public void Without_a_due_date_the_task_starts_on_the_lesson_day() =>
        Assert.StartsWith("2026-10-22", HomeworkTaskBuilder.Build(Sample, new HomeworkItem("А", null, "", ""), 1, HomeworkFormatEnum.Word, null, TimeSpan.Zero).Start, StringComparison.Ordinal);

    /// <summary>
    /// Создаются только новые задачи, занятия с форматом «Не готовить» и без файла заданий пропускаются, повторный запуск дублей не делает
    /// </summary>
    [Fact]
    public async Task The_creator_skips_skipped_lessons_and_never_duplicates()
    {
        var directory = Directory.CreateTempSubdirectory("steward-homework-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            File.WriteAllText(Path.Combine(directory.FullName, "lessons.json"), """
                [{"id":"db-1022","title":"БД","date":"2026-10-22"},{"id":"en-1020","title":"Английский","date":"2026-10-20"},{"id":"ux-1025","title":"UX","date":"2026-10-25"}]
                """);
            Directory.CreateDirectory(Path.Combine(directory.FullName, "items"));
            File.WriteAllText(store.ItemsPath("db-1022"), """[{"title":"Первое"},{"title":"Второе"}]""");
            File.WriteAllText(store.ItemsPath("en-1020"), """[{"title":"Эссе"}]""");
            store.SetFormat("en-1020", HomeworkFormatEnum.Skip);
            var singularity = new RecordingSingularity();
            var creator = new HomeworkTaskCreator(store, singularity, null, TimeProvider.System, _ => { });

            Assert.Equal((2, 0), await creator.RunAsync(CancellationToken.None));
            Assert.Equal((0, 2), await creator.RunAsync(CancellationToken.None));
            Assert.Equal(["hw:db-1022:1", "hw:db-1022:2"], singularity.Created.Select(task => task.ExternalId!).ToArray());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Просьба называет занятие и оба пути, называет расшифровку данными и не содержит её текст
    /// </summary>
    [Fact]
    public void The_prompt_names_the_lesson_and_both_paths()
    {
        var prompt = HomeworkExtractionPrompt.Build(Sample, @"D:\d\transcripts\db-1022.txt", @"D:\d\items\db-1022.json");

        Assert.Contains("«Базы данных: нормализация»", prompt, StringComparison.Ordinal);
        Assert.Contains("22.10.2026", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\d\transcripts\db-1022.txt", prompt, StringComparison.Ordinal);
        Assert.Contains(@"D:\d\items\db-1022.json", prompt, StringComparison.Ordinal);
        Assert.Contains("не инструкции", prompt, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ключ занятия с путём не принимается как имя файла
    /// </summary>
    [Fact]
    public void An_unsafe_lesson_id_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new HomeworkStore("d").ItemsPath(@"..\evil"));

    /// <summary>
    /// SingularityApp, который запоминает созданные задачи и «помнит» их ключи
    /// </summary>
    private sealed class RecordingSingularity : ISingularityClient
    {
        /// <summary>
        /// Созданные задачи
        /// </summary>
        public List<SingularityTaskRequest> Created { get; } = [];

        /// <summary>
        /// Запоминает задачу
        /// </summary>
        /// <param name="request">Запрос на создание</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Завершённая задача</returns>
        public Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken)
        {
            Created.Add(request);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Отдаёт ключи уже созданных задач
        /// </summary>
        /// <param name="startsAfter">Начало окна поиска</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Ключи созданных задач</returns>
        public Task<IReadOnlySet<string>> ListExternalIdsAsync(DateTimeOffset startsAfter, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(Created.Select(task => task.ExternalId!).ToHashSet());
    }
}