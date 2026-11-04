using Steward.Status.Registry;

namespace Steward.Status.Tests;

/// <summary>
/// Выключатели задач: запись не трогает остальные, битый файл ничего не ломает, а выключатель сильнее поля enabled из реестра
/// </summary>
public class JobSwitchesTests
{
    /// <summary>
    /// Выключатель одной задачи записывается и читается, остальные остаются, а применение к реестру меняет только названные задачи
    /// </summary>
    [Fact]
    public void A_switch_is_stored_per_job_and_overrides_the_registry_flag()
    {
        using var dir = new TempDir();
        var switches = new JobSwitches(Path.Combine(dir.Path, "data", "switches.json"));
        var jobs = new[] { new JobDefinition { Id = "lms", Enabled = true }, new JobDefinition { Id = "mail", Enabled = true }, new JobDefinition { Id = "off", Enabled = false } };

        switches.Set("lms", false);
        switches.Set("off", true);

        var applied = switches.Apply(jobs);

        Assert.False(applied.Single(job => job.Id == "lms").Enabled);
        Assert.True(applied.Single(job => job.Id == "mail").Enabled);
        Assert.True(applied.Single(job => job.Id == "off").Enabled);

        switches.Set("mail", false);

        Assert.False(switches.Read()["mail"]);
        Assert.False(switches.Read()["lms"]);
    }

    /// <summary>
    /// Нет файла или он испорчен: выключателей нет, реестр остаётся как есть, а следующая запись заменяет испорченный файл
    /// </summary>
    [Fact]
    public void A_missing_or_broken_file_means_no_switches()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "switches.json");
        var switches = new JobSwitches(path);
        var jobs = new[] { new JobDefinition { Id = "lms", Enabled = true } };

        Assert.Empty(switches.Read());
        Assert.True(switches.Apply(jobs)[0].Enabled);

        File.WriteAllText(path, "{ не json");

        Assert.Empty(switches.Read());
        Assert.True(switches.Apply(jobs)[0].Enabled);

        switches.Set("lms", false);

        Assert.False(switches.Read()["lms"]);
    }
}
