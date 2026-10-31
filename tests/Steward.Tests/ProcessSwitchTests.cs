using Steward.Models;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Тумблер процесса на карточке «Статус»: показывается у процессов, отражает состояние и просит переключить на противоположное
/// </summary>
public class ProcessSwitchTests
{
    /// <summary>
    /// Включённый процесс просит выключения, выключенный включения, а карточка без обработчика тумблера не показывает
    /// </summary>
    [Fact]
    public void The_switch_asks_for_the_opposite_state_and_is_hidden_without_a_handler()
    {
        var calls = new List<(string Id, bool Enabled)>();
        var on = new TaskCardViewModel(new TaskStatusRow("lms", "Автоматизация образования", "Работает", string.Empty, PanelToneEnum.Running), _ => { }, setEnabled: (id, enabled) => calls.Add((id, enabled)));
        var off = new TaskCardViewModel(new TaskStatusRow("mail", "Почта", "Отключена", string.Empty, PanelToneEnum.Idle, Muted: true), _ => { }, setEnabled: (id, enabled) => calls.Add((id, enabled)));
        var plain = new TaskCardViewModel(new TaskStatusRow("schedule", "Расписание", "Работает", string.Empty, PanelToneEnum.Running), _ => { });

        Assert.True(on.CanSwitch);
        Assert.True(on.IsOn);
        Assert.False(off.IsOn);
        Assert.False(plain.CanSwitch);

        on.SwitchCommand.Execute(null);
        off.SwitchCommand.Execute(null);

        Assert.Equal([("lms", false), ("mail", true)], calls);
    }
}
