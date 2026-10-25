using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет, когда на карточке задачи показывается вращение, галочка и обычная точка
/// </summary>
public class ActivityTrackingTests
{
    /// <summary>
    /// Пока задача идёт, показывается вращение, а когда она закончилась успешно, галочка
    /// </summary>
    [Fact]
    public void A_job_that_finishes_well_shows_a_tick()
    {
        var model = new MainViewModel();

        model.Apply(new SyncSnapshot(), [Row(RowToneEnum.Working)], [], []);
        Assert.Equal(ActivityStateEnum.Working, model.Tasks.Single(task => task.Name == "Почта").Activity);

        model.Apply(new SyncSnapshot(), [Row(RowToneEnum.Ok)], [], []);
        var done = model.Tasks.Single(task => task.Name == "Почта");
        Assert.Equal(ActivityStateEnum.Done, done.Activity);
        Assert.True(done.ActivitySince > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    /// <summary>
    /// Сбойной задаче галочка не показывается
    /// </summary>
    [Fact]
    public void A_job_that_fails_shows_no_tick()
    {
        var model = new MainViewModel();

        model.Apply(new SyncSnapshot(), [Row(RowToneEnum.Working)], [], []);
        model.Apply(new SyncSnapshot(), [Row(RowToneEnum.Problem)], [], []);

        Assert.Equal(ActivityStateEnum.None, model.Tasks.Single(task => task.Name == "Почта").Activity);
    }

    /// <summary>
    /// Задача, которая не запускалась, остаётся с обычной точкой
    /// </summary>
    [Fact]
    public void An_idle_job_keeps_the_plain_dot()
    {
        var model = new MainViewModel();

        model.Apply(new SyncSnapshot(), [Row(RowToneEnum.Ok)], [], []);

        Assert.Equal(ActivityStateEnum.None, model.Tasks.Single(task => task.Name == "Почта").Activity);
    }

    /// <summary>
    /// Собирает строку задачи «Почта» с заданным цветом
    /// </summary>
    /// <param name="tone">Цвет строки</param>
    /// <returns>Строка задачи</returns>
    private static JobRow Row(RowToneEnum tone) => new("mail", "Почта", "", "", tone, 0, true);
}
