namespace Steward.Status.Tests;

/// <summary>
/// Проверяет чтение списка задач и поиск папки данных
/// </summary>
public class RegistryAndLocationTests
{
    /// <summary>
    /// Файл с комментариями и подстановкой корня репозитория читается, плохая задача пропускается с понятной ошибкой
    /// </summary>
    [Fact]
    public void Registry_loads_good_jobs_and_reports_the_bad_ones()
    {
        const string json = """
            {
              // комментарии разрешены
              "jobs": [
                {
                  "id": "mail", "title": "Почта", "enabled": false,
                  "command": "{repo}\\Steward.Mail\\publish\\Steward.Mail.exe",
                  "triggers": [ { "type": "start" }, { "type": "daily", "at": "09:00" }, ],
                  "staleAfterMinutes": 960
                },
                { "id": "Bad Id", "command": "x" },
                { "id": "no-command" },
                { "id": "bad-time", "command": "x", "triggers": [ { "type": "daily", "at": "9am" } ] },
                { "id": "bad-type", "command": "x", "triggers": [ { "type": "weekly" } ] },
                { "id": "bad-interval", "command": "x", "triggers": [ { "type": "interval" } ] },
                { "id": "mail", "command": "x" }
              ]
            }
            """;

        var result = JobRegistry.Parse(json, @"D:\Repo");

        var mail = Assert.Single(result.Jobs);
        Assert.Equal(@"D:\Repo\Steward.Mail\publish\Steward.Mail.exe", mail.Command);
        Assert.False(mail.Enabled);
        Assert.Equal(960, mail.StaleAfterMinutes);
        Assert.Equal([JobTrigger.START, JobTrigger.DAILY], mail.Triggers.Select(trigger => trigger.Type).ToArray());
        Assert.Equal(6, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Contains("Bad Id", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("идентификатор уже занят", StringComparison.Ordinal));
    }

    /// <summary>
    /// Если корень репозитория неизвестен, задача с {repo} отвергается, а остальные работают
    /// </summary>
    [Fact]
    public void Registry_rejects_the_repo_token_when_the_root_is_unknown()
    {
        const string json = """{"jobs":[{"id":"a","command":"{repo}\\a.exe"},{"id":"b","command":"b.exe"}]}""";

        var result = JobRegistry.Parse(json, repoRoot: null);

        Assert.Equal(["b"], result.Jobs.Select(job => job.Id).ToArray());
        Assert.Single(result.Errors);
    }

    /// <summary>
    /// Нет файла — нет задач и нет ошибок, а сломанный JSON даёт одну ошибку
    /// </summary>
    [Fact]
    public void Registry_handles_a_missing_and_a_broken_file()
    {
        using var dir = new TempDir();
        var broken = Path.Combine(dir.Path, "jobs.json");
        File.WriteAllText(broken, "{ jobs: [");

        var missing = JobRegistry.Load(Path.Combine(dir.Path, "none.json"), null);
        var bad = JobRegistry.Load(broken, null);

        Assert.Empty(missing.Jobs);
        Assert.Empty(missing.Errors);
        Assert.Empty(bad.Jobs);
        Assert.Single(bad.Errors);
    }

    /// <summary>
    /// Корень репозитория находится по файлу решения, а папка данных берётся из явного пути, окружения или корня
    /// </summary>
    [Fact]
    public void Location_prefers_explicit_then_environment_then_the_repository()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "Steward.slnx"), "<Solution/>");
        var deep = Directory.CreateDirectory(Path.Combine(dir.Path, "Steward.Mail", "publish")).FullName;
        var saved = Environment.GetEnvironmentVariable(DataLocation.DATA_ENVIRONMENT_VARIABLE);
        try
        {
            Environment.SetEnvironmentVariable(DataLocation.DATA_ENVIRONMENT_VARIABLE, null);
            Assert.Equal(dir.Path, DataLocation.FindRepoRoot(deep));
            Assert.Equal(Path.Combine(dir.Path, "data"), DataLocation.DataDirectory(null, deep));
            Assert.Equal(Path.Combine(dir.Path, "data", "status"), DataLocation.StatusDirectory(null, deep));
            Assert.Equal(Path.Combine(dir.Path, "config", "jobs.json"), DataLocation.JobsFile(deep));

            Environment.SetEnvironmentVariable(DataLocation.DATA_ENVIRONMENT_VARIABLE, @"D:\FromEnvironment");
            Assert.Equal(@"D:\FromEnvironment", DataLocation.DataDirectory(null, deep));
            Assert.Equal(@"D:\Explicit", DataLocation.DataDirectory(@"D:\Explicit", deep));
        }
        finally
        {
            Environment.SetEnvironmentVariable(DataLocation.DATA_ENVIRONMENT_VARIABLE, saved);
        }
    }
}
