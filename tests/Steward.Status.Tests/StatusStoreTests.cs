using Steward.Status.State;

namespace Steward.Status.Tests;

/// <summary>
/// Проверяет формат файла статуса и безопасное чтение и запись
/// </summary>
public class StatusStoreTests
{
    /// <summary>
    /// Статус записывается и читается без потерь, а состояние лежит строкой в camelCase
    /// </summary>
    [Fact]
    public void Write_then_read_keeps_every_field_and_writes_the_state_as_text()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);
        var status = new ProcessStatus
        {
            Id = "mail",
            Title = "Почта",
            State = ProcessStateEnum.NeedsAttention,
            UpdatedAt = Samples.Winter(2026, 10, 25, 9),
            LastRunAt = Samples.Winter(2026, 10, 25, 9),
            LastSuccessAt = Samples.Winter(2026, 10, 24, 19),
            NextRunAt = Samples.Winter(2026, 10, 25, 19),
            Summary = "Ответить: 1",
            Error = null,
            Awaiting = 2,
        };

        store.Write(status);

        Assert.Equal(status, store.Read("mail"));
        var text = File.ReadAllText(Path.Combine(dir.StatusPath, "mail.json"));
        Assert.Contains("\"state\": \"needsAttention\"", text, StringComparison.Ordinal);
        Assert.Contains("\"schemaVersion\": 1", text, StringComparison.Ordinal);
        Assert.Contains("\"title\": \"Почта\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// После записи в папке нет временных файлов, а повторная запись заменяет старый
    /// </summary>
    [Fact]
    public void Write_replaces_the_file_and_leaves_no_temporary_files()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);

        store.Write(Samples.Status("job", ProcessStateEnum.Running, Samples.Winter(2026, 10, 25, 9)));
        store.Write(Samples.Status("job", ProcessStateEnum.Ok, Samples.Winter(2026, 10, 25, 10)));

        Assert.Equal(ProcessStateEnum.Ok, store.Read("job")!.State);
        Assert.Equal(["job.json"], Directory.GetFiles(dir.StatusPath).Select(path => Path.GetFileName(path)!).ToArray());
    }

    /// <summary>
    /// Повреждённый файл не мешает прочитать остальные
    /// </summary>
    [Fact]
    public void ReadAll_skips_a_corrupt_file_and_orders_the_rest()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);
        store.Write(Samples.Status("b-job", ProcessStateEnum.Ok, Samples.Winter(2026, 10, 25, 9)));
        store.Write(Samples.Status("a-job", ProcessStateEnum.Idle, Samples.Winter(2026, 10, 25, 9)));
        File.WriteAllText(Path.Combine(dir.StatusPath, "broken.json"), "{ это не json");
        File.WriteAllText(Path.Combine(dir.StatusPath, "no-id.json"), "{ \"state\": \"ok\" }");

        Assert.Equal(["a-job", "b-job"], store.ReadAll().Select(status => status.Id).ToArray());
        Assert.Null(store.Read("broken"));
    }

    /// <summary>
    /// Состояние из будущей версии формата читается как неизвестное, а не роняет чтение
    /// </summary>
    [Fact]
    public void Read_treats_an_unknown_state_as_unknown()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(dir.StatusPath);
        File.WriteAllText(Path.Combine(dir.StatusPath, "future.json"), """{"id":"future","state":"hibernating","updatedAt":"2026-10-25T09:00:00+01:00","somethingNew":42}""");

        var status = new StatusStore(dir.StatusPath).Read("future");

        Assert.NotNull(status);
        Assert.Equal(ProcessStateEnum.Unknown, status.State);
    }

    /// <summary>
    /// Идентификатор с путём не превращается в имя файла
    /// </summary>
    /// <param name="id">Идентификатор, который не должен быть принят</param>
    [Theory]
    [InlineData("..\\evil")]
    [InlineData("a/b")]
    [InlineData("Mail")]
    [InlineData("")]
    [InlineData("with space")]
    public void An_id_that_could_escape_the_folder_is_rejected(string id)
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);

        Assert.False(StatusStore.IsValidId(id));
        Assert.Throws<ArgumentException>(() => store.Read(id));
        Assert.Throws<ArgumentException>(() => store.Write(Samples.Status(id, ProcessStateEnum.Ok, DateTimeOffset.Now)));
    }

    /// <summary>
    /// Нет папки — нет статусов
    /// </summary>
    [Fact]
    public void ReadAll_is_empty_when_the_folder_does_not_exist()
    {
        using var dir = new TempDir();

        Assert.Empty(new StatusStore(Path.Combine(dir.Path, "missing")).ReadAll());
    }

    /// <summary>
    /// Плохой идентификатор при безопасном чтении даёт пустой результат, а не исключение
    /// </summary>
    [Fact]
    public void TryRead_returns_null_for_a_bad_id_and_for_a_missing_file()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);

        Assert.Null(store.TryRead("..\\evil"));
        Assert.Null(store.TryRead("missing"));
    }

    /// <summary>
    /// Сбой не стирает время последнего успеха и число ожидающих решения
    /// </summary>
    [Fact]
    public void Report_keeps_the_last_success_and_the_awaiting_count_after_a_failure()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);
        var success = Samples.Winter(2026, 10, 25, 9);
        store.Write(Samples.Status("job", ProcessStateEnum.Ok, success) with { LastSuccessAt = success, Awaiting = 3 });

        store.Report("job", "Задача", ProcessStateEnum.Failed, Samples.Winter(2026, 10, 25, 19), null, "упала", success: false);

        var status = store.Read("job")!;
        Assert.Equal(ProcessStateEnum.Failed, status.State);
        Assert.Equal(success, status.LastSuccessAt);
        Assert.Equal(Samples.Winter(2026, 10, 25, 19), status.LastRunAt);
        Assert.Equal(3, status.Awaiting);
    }

    /// <summary>
    /// Запись «выполняется» не меняет время последнего запуска, пока запуск не закончился
    /// </summary>
    [Fact]
    public void Report_running_keeps_the_previous_last_run()
    {
        using var dir = new TempDir();
        var store = new StatusStore(dir.StatusPath);
        var earlier = Samples.Winter(2026, 10, 25, 9);
        store.Write(Samples.Status("job", ProcessStateEnum.Ok, earlier) with { LastRunAt = earlier });

        store.Report("job", "Задача", ProcessStateEnum.Running, Samples.Winter(2026, 10, 25, 19), "Запуск", null, success: false);

        Assert.Equal(earlier, store.Read("job")!.LastRunAt);
    }}
