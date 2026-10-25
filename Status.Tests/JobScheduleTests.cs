namespace SingularityTools.Status.Tests;

/// <summary>
/// Проверяет расчёт времени запуска, в том числе в день перехода на зимнее время
/// </summary>
public class JobScheduleTests
{
    /// <summary>
    /// Запуск в 09:00 в день перехода на зимнее время наступает по новому смещению, а не по вчерашнему
    /// </summary>
    [Fact]
    public void Daily_run_after_the_autumn_change_uses_the_new_offset()
    {
        var job = Samples.Job("mail", Samples.Daily("09:00"));
        var now = Samples.Summer(2026, 10, 24, 9, 1);

        var next = JobSchedule.NextRun(job, Samples.Summer(2026, 10, 24, 9), now, Samples.Belgrade);

        Assert.Equal(Samples.Winter(2026, 10, 25, 9), next);
        Assert.Equal(TimeSpan.FromHours(1), next!.Value.Offset);
    }

    /// <summary>
    /// Время, которое при переходе на зимнее время бывает дважды, срабатывает один раз и по первому разу
    /// </summary>
    [Fact]
    public void Daily_run_in_the_repeated_hour_fires_once()
    {
        var job = Samples.Job("night", Samples.Daily("02:30"));
        var zone = Samples.Belgrade;

        var first = JobSchedule.NextRun(job, null, Samples.Summer(2026, 10, 25, 0, 0), zone);
        var afterFirst = JobSchedule.NextRun(job, first, Samples.Summer(2026, 10, 25, 2, 40), zone);
        var secondPass = JobSchedule.DueReason(job, first, Samples.Winter(2026, 10, 25, 2, 35), zone);

        Assert.Equal(Samples.Summer(2026, 10, 25, 2, 30), first);
        Assert.Equal(Samples.Winter(2026, 10, 26, 2, 30), afterFirst);
        Assert.Null(secondPass);
    }

    /// <summary>
    /// Времени, которого весной не существует, запуск сдвигается на час вперёд
    /// </summary>
    [Fact]
    public void Daily_run_in_the_skipped_spring_hour_moves_forward()
    {
        var job = Samples.Job("night", Samples.Daily("02:30"));

        var next = JobSchedule.NextRun(job, null, Samples.Winter(2026, 3, 29, 0, 0), Samples.Belgrade);

        Assert.Equal(Samples.Summer(2026, 3, 29, 3, 30), next);
    }

    /// <summary>
    /// Пропущенный дневной запуск наверстывается один раз, а выполненный не повторяется
    /// </summary>
    [Fact]
    public void A_missed_daily_run_is_caught_up_once()
    {
        var job = Samples.Job("mail", Samples.Daily("09:00"));
        var zone = Samples.Belgrade;
        var now = Samples.Winter(2026, 10, 25, 10);

        Assert.Equal(JobTrigger.DAILY, JobSchedule.DueReason(job, Samples.Winter(2026, 10, 24, 19), now, zone));
        Assert.Equal(JobTrigger.DAILY, JobSchedule.DueReason(job, null, now, zone));
        Assert.Null(JobSchedule.DueReason(job, Samples.Winter(2026, 10, 25, 9, 0).AddSeconds(30), now, zone));
    }

    /// <summary>
    /// До времени запуска задача не просыпается: вчерашний запуск ещё действует
    /// </summary>
    [Fact]
    public void A_daily_job_does_not_run_before_its_time()
    {
        var job = Samples.Job("mail", Samples.Daily("19:00"));

        Assert.Null(JobSchedule.DueReason(job, Samples.Winter(2026, 10, 24, 19, 1), Samples.Winter(2026, 10, 25, 10), Samples.Belgrade));
    }

    /// <summary>
    /// Интервал считается от прошлого запуска
    /// </summary>
    [Fact]
    public void An_interval_counts_from_the_last_run()
    {
        var job = Samples.Job("poll", Samples.Every(30));
        var zone = Samples.Belgrade;
        var now = Samples.Winter(2026, 10, 25, 12);

        Assert.Null(JobSchedule.DueReason(job, now.AddMinutes(-10), now, zone));
        Assert.Equal(JobTrigger.INTERVAL, JobSchedule.DueReason(job, now.AddMinutes(-31), now, zone));
        Assert.Equal(now.AddMinutes(20), JobSchedule.NextRun(job, now.AddMinutes(-10), now, zone));
        Assert.Null(JobSchedule.NextRun(Samples.Job("manual-only", Samples.OnStart), null, now, zone));
    }

    /// <summary>
    /// Из нескольких времён выбирается ближайшее
    /// </summary>
    [Fact]
    public void The_nearest_of_several_times_wins()
    {
        var job = Samples.Job("mail", Samples.Daily("09:00"), Samples.Daily("19:00"));

        var next = JobSchedule.NextRun(job, Samples.Winter(2026, 10, 25, 9), Samples.Winter(2026, 10, 25, 12), Samples.Belgrade);

        Assert.Equal(Samples.Winter(2026, 10, 25, 19), next);
    }
}
