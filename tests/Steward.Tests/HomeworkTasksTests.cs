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
    /// Задача встаёт за два дня до срока, срок за день до него, ключ устойчивый, формат и требования видны
    /// </summary>
    [Fact]
    public void The_task_starts_two_days_before_the_due_day_with_a_stable_key()
    {
        var task = HomeworkTaskBuilder.Build(Sample, new HomeworkItem("Схема до 3НФ", new DateOnly(2026, 10, 29), "Диаграмма", "https://lms.example/1"), 2, HomeworkFormatEnum.Presentation, "proj", TimeSpan.FromHours(3), today: Sample.Date);

        Assert.Equal("ДЗ: Схема до 3НФ", task.Title);
        Assert.Equal("2026-10-27T00:00:00+03:00", task.Start);
        Assert.Equal("2026-10-28T00:00:00+03:00", task.Deadline);
        Assert.Equal("hw:db-1022:2", task.ExternalId);
        Assert.Equal("proj", task.ProjectId);
        Assert.Contains("Презентация", task.Note, StringComparison.Ordinal);
        Assert.Contains("Диаграмма", task.Note, StringComparison.Ordinal);
        Assert.Contains("https://lms.example/1", task.Note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Без срока и без следующего занятия задача начинается в день занятия, а срока у неё нет
    /// </summary>
    [Fact]
    public void Without_a_due_date_the_task_starts_on_the_lesson_day() =>
        Assert.StartsWith("2026-10-22", HomeworkTaskBuilder.Build(Sample, new HomeworkItem("А", null, "", ""), 1, HomeworkFormatEnum.Word, null, TimeSpan.Zero).Start, StringComparison.Ordinal);

    /// <summary>
    /// Домашнее с понедельника сдаётся на следующем понедельнике: задача встаёт в субботу, срок воскресенье. Если до занятия меньше двух дней, задача встаёт сегодня
    /// </summary>
    [Fact]
    public void The_task_is_planned_before_the_next_lesson_of_the_subject()
    {
        var monday = new DateOnly(2026, 11, 2);
        var lesson = new Lesson("gd-1102", "Гейм-дизайн", monday);
        var item = new HomeworkItem("Отчёт", null, "", "");

        var weekAhead = HomeworkTaskBuilder.Build(lesson, item, 1, HomeworkFormatEnum.Word, null, TimeSpan.Zero, nextLesson: new DateOnly(2026, 11, 9), today: monday);
        var lastMinute = HomeworkTaskBuilder.Build(lesson, item, 1, HomeworkFormatEnum.Word, null, TimeSpan.Zero, nextLesson: new DateOnly(2026, 11, 9), today: new DateOnly(2026, 11, 8));

        Assert.StartsWith("2026-11-07", weekAhead.Start, StringComparison.Ordinal);
        Assert.StartsWith("2026-11-08", weekAhead.Deadline, StringComparison.Ordinal);
        Assert.StartsWith("2026-11-08", lastMinute.Start, StringComparison.Ordinal);
        Assert.StartsWith("2026-11-08", lastMinute.Deadline, StringComparison.Ordinal);
    }

    /// <summary>
    /// Следующее занятие берётся из расписания: тот же предмет, любой тип занятия, позже заданного дня, удалённые и чужие предметы пропускаются
    /// </summary>
    [Fact]
    public void The_next_lesson_comes_from_the_schedule_of_the_same_subject()
    {
        static SingularityTaskInfo Entry(string id, string title, string start, bool removed = false) =>
            new(id, "up:g:" + id, 0, removed, false, title, DateTimeOffset.Parse(start, System.Globalization.CultureInfo.InvariantCulture));

        var schedule = new[]
        {
            Entry("1", "Гейм-дизайн (Практика)", "2026-11-02T10:00:00+00:00"),
            Entry("2", "Философия", "2026-11-04T10:00:00+00:00"),
            Entry("3", "Гейм-дизайн (Лекция)", "2026-11-16T10:00:00+00:00"),
            Entry("4", "Гейм-дизайн (Практика)", "2026-11-09T10:00:00+00:00", removed: true),
            Entry("5", "Гейм-дизайн (Практика)", "2026-11-12T10:00:00+00:00"),
        };

        Assert.Equal(new DateOnly(2026, 11, 12), HomeworkDeadline.NextLesson("Гейм-дизайн", new DateOnly(2026, 11, 2), schedule));
        Assert.Null(HomeworkDeadline.NextLesson("Математика", new DateOnly(2026, 11, 2), schedule));
    }

    /// <summary>
    /// Задача, удалённая в Singularity или пропавшая оттуда, забывается, и кнопка добавления снова доступна; живая и выполненная остаются, а удалённая попадает в список «не заводить само»
    /// </summary>
    [Fact]
    public void A_task_deleted_in_singularity_is_forgotten_so_the_button_returns()
    {
        var directory = Directory.CreateTempSubdirectory("steward-homework-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            store.MarkCreated(["hw:db-1022:1", "hw:db-1022:2", "hwq:db-1022:3", "hw:db-1022:4"]);
            SingularityTaskInfo[] tasks =
            [
                new("T-1", "hw:db-1022:1", 0),
                new("T-2", "hw:db-1022:2", 0, Removed: true),
                new("T-4", "hw:db-1022:4", 1),
            ];

            var gone = HomeworkSingularitySync.Forget(store, [.. store.LoadCreated()], tasks);

            Assert.Equal(["hw:db-1022:2", "hwq:db-1022:3"], gone.Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(["hw:db-1022:1", "hw:db-1022:4"], store.LoadCreated().Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(["hw:db-1022:2"], HomeworkSingularitySync.DeletedKeys(tasks).ToArray());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Папка единого проекта предмета записывается в настройки, читается как строка состояния проекта (скрипты и время правки, вложенная папка Unity находится) и попадает в просьбу агенту как папка только для чтения
    /// </summary>
    [Fact]
    public void The_subject_project_folder_is_stored_shown_and_given_to_the_agent()
    {
        var directory = Directory.CreateTempSubdirectory("steward-project-folder-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var repo = Path.Combine(directory.FullName, "Игра");
            var assets = Path.Combine(repo, "Игра", "Assets", "Scripts");
            Directory.CreateDirectory(assets);
            File.WriteAllText(Path.Combine(assets, "Hero.cs"), "// герой");
            File.WriteAllText(Path.Combine(assets, "Hero.cs.meta"), "meta");
            File.WriteAllText(Path.Combine(assets, "Menu.cs"), "// меню");

            store.SetSubjectProjectFolder("Unity", repo);
            store.SetSubjectAutoPrepare("Unity", true);

            var options = store.SubjectOptionsFor("Unity");
            Assert.Equal(repo, options.ProjectFolder);
            Assert.True(options.AutoPrepare);

            var status = ProjectStatus.Read(repo);
            Assert.True(status.Exists);
            Assert.Equal("Игра", status.Name);
            Assert.Equal(2, status.Scripts);
            Assert.StartsWith("Проект Игра: скриптов 2 · изменён ", status.Text, StringComparison.Ordinal);
            Assert.StartsWith("Папка проекта не найдена", ProjectStatus.Read(Path.Combine(directory.FullName, "нет")).Text, StringComparison.Ordinal);

            var prompt = DraftBuilder.Prompt(Sample, new HomeworkItem("Меню", null, "", ""), 1, HomeworkFormatEnum.Project, string.Empty, @"D:\result", projectFolder: repo);
            Assert.Contains(repo, prompt, StringComparison.Ordinal);
            Assert.Contains("только на чтение", prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Единый проект", DraftBuilder.Prompt(Sample, new HomeworkItem("Меню", null, "", ""), 1, HomeworkFormatEnum.Project, string.Empty, @"D:\result"), StringComparison.Ordinal);

            store.SetSubjectProjectFolder("Unity", string.Empty);
            Assert.Null(store.SubjectOptionsFor("Unity").ProjectFolder);
            Assert.True(store.SubjectOptionsFor("Unity").AutoPrepare);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>
    /// В просьбу идут файлы условия только этого задания, а не всех заданий предмета, а язык работы берётся из настроек предмета: английский предмет пишется по-английски, остальные по-русски
    /// </summary>
    [Fact]
    public async Task The_prompt_carries_only_the_item_assignment_files_and_the_subject_language()
    {
        var directory = Directory.CreateTempSubdirectory("steward-prompt-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            var own = Path.Combine(directory.FullName, "assignments", "111", "assignment.md");
            var other = Path.Combine(directory.FullName, "assignments", "222", "assignment.md");
            var prompts = new List<string>();
            DraftBuilder.AgentRun agent = (prompt, _, _, _) =>
            {
                prompts.Add(prompt);

                return Task.FromResult("стоп");
            };

            await DraftBuilder.BuildAsync(store, Sample, [new HomeworkItem("Эссе", null, "", "", ActivityId: "111")], HomeworkFormatEnum.Text, agent, CancellationToken.None, assignments: [own, other], language: "английском");
            await DraftBuilder.BuildAsync(store, Sample, [new HomeworkItem("Эссе", null, "", "")], HomeworkFormatEnum.Text, agent, CancellationToken.None, assignments: [own, other]);

            Assert.Contains(own, prompts[0], StringComparison.Ordinal);
            Assert.DoesNotContain(other, prompts[0], StringComparison.Ordinal);
            Assert.Contains("на английском языке", prompts[0], StringComparison.Ordinal);
            Assert.Contains(other, prompts[1], StringComparison.Ordinal);
            Assert.Contains("на русском языке", prompts[1], StringComparison.Ordinal);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

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
    /// Созданные задачи запоминаются, и страница видит, по каким заданиям задача уже есть, а по каким ещё нет
    /// </summary>
    [Fact]
    public async Task Created_tasks_are_remembered_and_shown_in_the_rows()
    {
        var directory = Directory.CreateTempSubdirectory("steward-homework-");
        try
        {
            var store = new HomeworkStore(directory.FullName);
            File.WriteAllText(Path.Combine(directory.FullName, "lessons.json"), """[{"id":"db-1022","title":"БД","date":"2026-10-22"}]""");
            Directory.CreateDirectory(Path.Combine(directory.FullName, "items"));
            File.WriteAllText(store.ItemsPath("db-1022"), """[{"title":"Первое"},{"title":"Второе"}]""");
            Assert.All(store.LoadRows(), row => Assert.False(row.Created));

            store.MarkCreated(["hw:db-1022:1"]);

            Assert.Equal([true, false], store.LoadRows().Select(row => row.Created).ToArray());

            await new HomeworkTaskCreator(store, new RecordingSingularity(), null, TimeProvider.System, _ => { }).RunAsync(CancellationToken.None);

            Assert.All(store.LoadRows(), row => Assert.True(row.Created));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
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