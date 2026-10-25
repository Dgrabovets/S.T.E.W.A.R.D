namespace Steward.Status.Tests;

/// <summary>
/// Проверяет, что строка задачи на панели говорит правду о её статусе
/// </summary>
public class JobRowTests
{
    /// <summary>
    /// Текущий момент в тестах: воскресенье, полдень
    /// </summary>
    private static readonly DateTimeOffset Now = Samples.Winter(2026, 10, 25, 12);

    /// <summary>
    /// Выключенная задача так и называется, даже если у неё есть статус
    /// </summary>
    [Fact]
    public void A_disabled_job_says_so()
    {
        var job = Samples.Job("mail") with { Enabled = false };

        var row = JobRowBuilder.Build(job, Samples.Status("mail", ProcessStateEnum.Ok, Now), null, Now);

        Assert.Equal("Отключена", row.Headline);
        Assert.Equal(RowToneEnum.Idle, row.Tone);
    }

    /// <summary>
    /// Пока файла нет, задача «ещё не запускалась» и называет время первого запуска
    /// </summary>
    [Fact]
    public void A_job_without_a_status_file_has_not_run_yet()
    {
        var row = JobRowBuilder.Build(Samples.Job("mail"), null, Samples.Winter(2026, 10, 25, 19), Now);

        Assert.Equal("Ещё не запускалась", row.Headline);
        Assert.Equal("Следующий запуск сегодня в 19:00", row.Detail);
        Assert.Equal(RowToneEnum.Idle, row.Tone);
    }

    /// <summary>
    /// Успешный запуск зелёный, со сводкой и временем следующего
    /// </summary>
    [Fact]
    public void A_healthy_run_shows_the_summary_and_the_next_run()
    {
        var status = Samples.Status("mail", ProcessStateEnum.Ok, Samples.Winter(2026, 10, 25, 9)) with { Summary = "Новых важных писем нет" };

        var row = JobRowBuilder.Build(Samples.Job("mail"), status, Samples.Winter(2026, 10, 26, 9), Now);

        Assert.Equal(RowToneEnum.Ok, row.Tone);
        Assert.Equal("Выполнено сегодня в 09:00", row.Headline);
        Assert.Equal("Новых важных писем нет. Следующий запуск завтра в 09:00", row.Detail);
    }

    /// <summary>
    /// Ошибка красная и несёт причину с временем
    /// </summary>
    [Fact]
    public void A_failed_run_shows_the_error()
    {
        var status = Samples.Status("mail", ProcessStateEnum.Failed, Samples.Winter(2026, 10, 24, 19)) with { Error = "Не задан пароль приложения" };

        var row = JobRowBuilder.Build(Samples.Job("mail"), status, null, Now);

        Assert.Equal(RowToneEnum.Problem, row.Tone);
        Assert.Equal("Не удалось", row.Headline);
        Assert.Equal("Не задан пароль приложения (вчера в 19:00)", row.Detail);
    }

    /// <summary>
    /// Выполняющаяся задача рабочая, а та, что «выполняется» дольше таймаута, считается зависшей
    /// </summary>
    [Fact]
    public void A_running_job_is_working_until_it_exceeds_its_timeout()
    {
        var job = Samples.Job("mail") with { TimeoutMinutes = 10 };

        var fresh = JobRowBuilder.Build(job, Samples.Status("mail", ProcessStateEnum.Running, Now.AddMinutes(-3)), null, Now);
        var hung = JobRowBuilder.Build(job, Samples.Status("mail", ProcessStateEnum.Running, Now.AddMinutes(-30)), null, Now);

        Assert.Equal((RowToneEnum.Working, "Выполняется"), (fresh.Tone, fresh.Headline));
        Assert.Equal((RowToneEnum.Problem, "Завис?"), (hung.Tone, hung.Headline));
    }

    /// <summary>
    /// Давно не было успешного запуска — проблема, даже если последний статус «ок»
    /// </summary>
    [Fact]
    public void A_stale_job_is_a_problem_even_when_the_last_status_is_ok()
    {
        var job = Samples.Job("mail") with { StaleAfterMinutes = 960 };
        var status = Samples.Status("mail", ProcessStateEnum.Ok, Samples.Winter(2026, 10, 24, 9)) with { LastSuccessAt = Samples.Winter(2026, 10, 24, 9) };

        var row = JobRowBuilder.Build(job, status, null, Now);

        Assert.Equal((RowToneEnum.Problem, "Давно не запускалась"), (row.Tone, row.Headline));
        Assert.Equal("Последний успех вчера в 09:00", row.Detail);
    }

    /// <summary>
    /// Результаты, которые ждут решения, выделяются отдельным цветом и считаются
    /// </summary>
    [Fact]
    public void Items_waiting_for_a_decision_are_flagged()
    {
        var status = Samples.Status("ideas", ProcessStateEnum.Ok, Samples.Winter(2026, 10, 25, 11)) with { Summary = "Разобрано 3 рилса", Awaiting = 3 };

        var row = JobRowBuilder.Build(Samples.Job("ideas"), status, null, Now);

        Assert.Equal(RowToneEnum.Attention, row.Tone);
        Assert.Equal(3, row.Awaiting);
        Assert.Equal("Разобрано 3 рилса. Ждут решения: 3", row.Detail);
    }

    /// <summary>
    /// Незнакомое состояние показывается нейтрально и не роняет панель
    /// </summary>
    [Fact]
    public void An_unknown_state_is_neutral()
    {
        var row = JobRowBuilder.Build(Samples.Job("future"), Samples.Status("future", ProcessStateEnum.Unknown, Now), null, Now);

        Assert.Equal((RowToneEnum.Idle, "Статус не распознан"), (row.Tone, row.Headline));
    }
}
