using Steward.Models;
using Steward.Status.Registry;
using Steward.Status.Running;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет, какая сфера достаётся задаче и записи журнала
/// </summary>
public class OrbKindTests
{
    /// <summary>
    /// Выполненная задача, которая ждёт следующего срока, получает ожидающую сферу, а работающая живую
    /// </summary>
    [Theory]
    [InlineData("Выполнено сегодня в 08:38", PanelToneEnum.Running, ActivityKindEnum.Waiting)]
    [InlineData("Работает", PanelToneEnum.Running, ActivityKindEnum.Alive)]
    [InlineData("Ждёт тебя", PanelToneEnum.Attention, ActivityKindEnum.Attention)]
    [InlineData("Не удалось", PanelToneEnum.Problem, ActivityKindEnum.Failed)]
    [InlineData("Не запускалась", PanelToneEnum.Idle, ActivityKindEnum.Dormant)]
    public void A_task_gets_the_orb_of_its_state(string headline, PanelToneEnum tone, ActivityKindEnum expected) =>
        Assert.Equal(expected, ActivityKinds.Of(new TaskStatusRow("mail", "Почта", headline, "", tone)));

    /// <summary>
    /// Выполняющейся задаче достаётся сфера по её делу, а отключённой тусклая
    /// </summary>
    [Fact]
    public void A_running_task_gets_its_own_motion_and_a_muted_one_is_dormant()
    {
        Assert.Equal(ActivityKindEnum.Connecting, ActivityKinds.Of(new TaskStatusRow("mail", "Почта", "Выполняется", "", PanelToneEnum.Running, Working: true)));
        Assert.Equal(ActivityKindEnum.Composing, ActivityKinds.Of(new TaskStatusRow("homework", "ДЗ", "Выполняется", "", PanelToneEnum.Running, Working: true)));
        Assert.Equal(ActivityKindEnum.Dormant, ActivityKinds.Of(new TaskStatusRow("mail", "Почта", "Отключена", "", PanelToneEnum.Idle, Muted: true)));
    }

    /// <summary>
    /// У каждого исхода запуска в журнале своя сфера и свой цвет, они не повторяют сферы статусов
    /// </summary>
    [Theory]
    [InlineData(RunOutcomeEnum.Ok, ActivityKindEnum.Sealed, PanelToneEnum.Running)]
    [InlineData(RunOutcomeEnum.Failed, ActivityKindEnum.Shattered, PanelToneEnum.Problem)]
    [InlineData(RunOutcomeEnum.TimedOut, ActivityKindEnum.Sweep, PanelToneEnum.Attention)]
    [InlineData(RunOutcomeEnum.NotStarted, ActivityKindEnum.Hollow, PanelToneEnum.Idle)]
    public void Each_run_outcome_has_its_own_orb(RunOutcomeEnum outcome, ActivityKindEnum kind, PanelToneEnum tone)
    {
        var start = new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero);
        var row = new RunRowViewModel(new RunRecord("mail", "Почта", JobTrigger.START, start, start.AddSeconds(5), 0, outcome, null), _ => { });

        Assert.Equal((kind, tone), (row.Kind, row.Tone));
    }
}