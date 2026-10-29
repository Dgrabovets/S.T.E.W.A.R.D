using Steward.Homework.Models;
using Steward.Homework.Services;
using Steward.Homework.Storage;
using Steward.Mail.Interfaces;
using Steward.Mail.Models;

namespace Steward.Lms.Tests;

/// <summary>
/// Автозавод задач ДЗ в SingularityApp: кого заводить, ссылка на готовую работу и выключенная запись
/// </summary>
public sealed class HomeworkAutoTasksTests : IDisposable
{
    /// <summary>
    /// Временная папка домашних заданий
    /// </summary>
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("steward-autotasks-");

    /// <summary>
    /// Хранилище во временной папке
    /// </summary>
    private readonly HomeworkStore _store;

    /// <summary>
    /// Создаёт хранилище с одним занятием
    /// </summary>
    public HomeworkAutoTasksTests()
    {
        _store = new HomeworkStore(_directory.FullName);
        File.WriteAllText(Path.Combine(_directory.FullName, "lessons.json"), """[{"id":"db-1022","title":"БД","date":"2026-10-22"}]""");
    }

    /// <summary>
    /// Удаляет временную папку
    /// </summary>
    public void Dispose() => _directory.Delete(recursive: true);

    /// <summary>
    /// Записывает файл заданий занятия
    /// </summary>
    /// <param name="json">Содержимое файла</param>
    private void WriteItems(string json)
    {
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "items"));
        File.WriteAllText(_store.ItemsPath("db-1022"), json);
    }

    /// <summary>
    /// Кладёт готовый файл задания в папку работ
    /// </summary>
    /// <param name="name">Имя файла</param>
    /// <returns>Путь к файлу</returns>
    private string WriteDraft(string name)
    {
        Directory.CreateDirectory(_store.DraftsDirectory("db-1022"));
        var path = Path.Combine(_store.DraftsDirectory("db-1022"), name);
        File.WriteAllText(path, "работа");

        return path;
    }

    /// <summary>
    /// Задания, которым нужно подтверждение, сданные и убранные в архив задач не получают, подтверждённые получают
    /// </summary>
    [Fact]
    public async Task Pending_confirmation_items_do_not_get_tasks()
    {
        WriteItems("""
            [
              {"title":"Ясное"},
              {"title":"Сомнительное","confidence":"low","evidence":"цитата"},
              {"title":"Сданное"},
              {"title":"Архивное"},
              {"title":"Подтверждённое","confidence":"low","evidence":"цитата"}
            ]
            """);
        _store.SetSubmitted("db-1022", 3, true);
        _store.SetArchived("db-1022", 4, true);
        _store.Confirm("db-1022", 5);
        var singularity = new StubSingularity();

        var (created, _) = await new HomeworkTaskCreator(_store, singularity, null, TimeProvider.System, _ => { }).RunAsync(CancellationToken.None);

        Assert.Equal(2, created);
        Assert.Equal(["hw:db-1022:1", "hw:db-1022:5"], singularity.Created.Select(task => task.ExternalId!).ToArray());
    }

    /// <summary>
    /// Сбой на одном задании не останавливает остальные, а неудавшееся не помечается созданным
    /// </summary>
    [Fact]
    public async Task One_failing_item_does_not_stop_the_others()
    {
        WriteItems("""[{"title":"А"},{"title":"Б"},{"title":"В"}]""");
        var singularity = new StubSingularity { FailOnCreate = "hw:db-1022:2" };
        var log = new List<string>();

        var (created, _) = await new HomeworkTaskCreator(_store, singularity, null, TimeProvider.System, log.Add).RunAsync(CancellationToken.None);

        Assert.Equal(2, created);
        Assert.DoesNotContain("hw:db-1022:2", _store.LoadCreated());
        Assert.Contains(log, line => line.Contains("Не удалось завести", StringComparison.Ordinal));
    }

    /// <summary>
    /// Выключенная запись: клиент не вызывается совсем, в журнале строки «Завёл бы», ничего не помечается созданным
    /// </summary>
    [Fact]
    public async Task Disabled_CreateTasks_only_logs()
    {
        WriteItems("""[{"title":"Первое"},{"title":"Сомнительное","confidence":"low","evidence":"цитата"}]""");
        WriteDraft("Задание 1.docx");
        var singularity = new StubSingularity();
        var log = new List<string>();

        var result = await new HomeworkTaskCreator(_store, singularity, null, TimeProvider.System, log.Add, dryRun: true).RunAsync(CancellationToken.None);
        var linked = await new HomeworkWorkLinker(_store, singularity, TimeProvider.System, log.Add, dryRun: true).RunAsync(CancellationToken.None);

        Assert.Equal((0, 0), result);
        Assert.Equal(0, linked);
        Assert.Equal(0, singularity.Calls);
        Assert.Empty(_store.LoadCreated());
        Assert.Contains(log, line => line.StartsWith("Завёл бы: ", StringComparison.Ordinal) && line.Contains("Первое", StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.Contains("Сомнительное", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ссылка на файл работы дописывается в уже созданную задачу один раз, повторный запуск ничего не меняет
    /// </summary>
    [Fact]
    public async Task Own_work_adds_a_link_to_the_existing_task()
    {
        WriteItems("""[{"title":"Первое"},{"title":"Второе"}]""");
        var path = WriteDraft("Задание 1.docx");
        WriteDraft("Задание 2.docx.partial");
        var singularity = new StubSingularity();
        singularity.Tasks.Add(new SingularityTaskInfo("t1", "hw:db-1022:1", 0));
        singularity.Tasks.Add(new SingularityTaskInfo("t2", "hw:db-1022:2", 0));
        var linker = new HomeworkWorkLinker(_store, singularity, TimeProvider.System, _ => { }, dryRun: false);

        Assert.Equal(1, await linker.RunAsync(CancellationToken.None));
        Assert.Equal(0, await linker.RunAsync(CancellationToken.None));

        var (taskId, note) = Assert.Single(singularity.Updates);
        Assert.Equal("t1", taskId);
        Assert.Contains(new Uri(path).AbsoluteUri, note, StringComparison.Ordinal);
        Assert.Contains("Задание 1.docx", note, StringComparison.Ordinal);
    }

    /// <summary>
    /// Сбой обновления заметки не ломает запуск и не помечает ссылку добавленной: следующий запуск попробует снова
    /// </summary>
    [Fact]
    public async Task A_failed_note_update_does_not_break_the_run_and_is_retried()
    {
        WriteItems("""[{"title":"Первое"}]""");
        WriteDraft("Задание 1.docx");
        var singularity = new StubSingularity { FailOnUpdate = true };
        singularity.Tasks.Add(new SingularityTaskInfo("t1", "hw:db-1022:1", 0));
        var linker = new HomeworkWorkLinker(_store, singularity, TimeProvider.System, _ => { }, dryRun: false);

        Assert.Equal(0, await linker.RunAsync(CancellationToken.None));

        singularity.FailOnUpdate = false;

        Assert.Equal(1, await linker.RunAsync(CancellationToken.None));
    }

    /// <summary>
    /// Клиент-заглушка: считает вызовы и ничего не отправляет в сеть
    /// </summary>
    private sealed class StubSingularity : ISingularityClient, ISingularityNotes
    {
        /// <summary>
        /// Сколько раз вызывали любой метод
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Созданные задачи
        /// </summary>
        public List<SingularityTaskRequest> Created { get; } = [];

        /// <summary>
        /// Задачи, которые «уже есть» в SingularityApp
        /// </summary>
        public List<SingularityTaskInfo> Tasks { get; } = [];

        /// <summary>
        /// Дописанные заметки: номер задачи и текст
        /// </summary>
        public List<(string TaskId, string Note)> Updates { get; } = [];

        /// <summary>
        /// Внешний ключ, при создании которого клиент падает
        /// </summary>
        public string? FailOnCreate { get; init; }

        /// <summary>
        /// Падать ли при обновлении заметки
        /// </summary>
        public bool FailOnUpdate { get; set; }

        /// <summary>
        /// Записывает задачу или падает на заданном ключе
        /// </summary>
        /// <param name="request">Поля задачи</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task CreateTaskAsync(SingularityTaskRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            if (request.ExternalId == FailOnCreate)
                throw new HttpRequestException("Singularity недоступен");

            Created.Add(request);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Отдаёт ключи созданных и заданных заранее задач
        /// </summary>
        /// <param name="startsAfter">Не используется</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Набор ключей</returns>
        public Task<IReadOnlySet<string>> ListExternalIdsAsync(DateTimeOffset startsAfter, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult<IReadOnlySet<string>>(Created.Select(task => task.ExternalId!).Concat(Tasks.Select(task => task.ExternalId)).ToHashSet(StringComparer.Ordinal));
        }

        /// <summary>
        /// Отдаёт заданные задачи с подходящим ключом
        /// </summary>
        /// <param name="startsAfter">Не используется</param>
        /// <param name="keyPrefix">Начало ключа</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Задачи</returns>
        public Task<IReadOnlyList<SingularityTaskInfo>> ListTasksAsync(DateTimeOffset startsAfter, string keyPrefix, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult<IReadOnlyList<SingularityTaskInfo>>([.. Tasks.Where(task => task.ExternalId.StartsWith(keyPrefix, StringComparison.Ordinal))]);
        }

        /// <summary>
        /// Запоминает дописанную заметку
        /// </summary>
        /// <param name="taskId">Номер задачи</param>
        /// <param name="note">Текст заметки</param>
        /// <param name="cancellationToken">Не используется</param>
        /// <returns>Завершённая задача</returns>
        public Task UpdateNoteAsync(string taskId, string note, CancellationToken cancellationToken)
        {
            Calls++;
            if (FailOnUpdate)
                throw new HttpRequestException("Singularity недоступен");

            Updates.Add((taskId, note));

            return Task.CompletedTask;
        }
    }
}
