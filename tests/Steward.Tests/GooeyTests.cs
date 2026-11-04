using System.Windows;
using Steward.Controls;
using Steward.Models;
using Steward.Monitoring.Models;
using Steward.Status.Rows;
using Steward.ViewModels;

namespace Steward.Tests;

/// <summary>
/// Проверяет перемычку между каплями и модель меню у иконки
/// </summary>
public class GooeyTests
{
    /// <summary>
    /// Близкие круги соединяются шейкой, которая лежит между ними
    /// </summary>
    [Fact]
    public void Close_circles_are_joined_by_a_neck_between_them()
    {
        var bridge = Metaball.Bridge(new Point(0, 0), 20, new Point(30, 0), 20);

        Assert.NotNull(bridge);
        Assert.InRange(bridge.Bounds.Left, -1, 21);
        Assert.InRange(bridge.Bounds.Right, 9, 31);
    }

    /// <summary>
    /// Далёкие круги и круг внутри другого перемычки не получают
    /// </summary>
    [Fact]
    public void Distant_or_nested_circles_get_no_neck()
    {
        Assert.Null(Metaball.Bridge(new Point(0, 0), 20, new Point(200, 0), 20));
        Assert.Null(Metaball.Bridge(new Point(0, 0), 30, new Point(5, 0), 10));
        Assert.Null(Metaball.Bridge(new Point(0, 0), 0, new Point(10, 0), 10));
    }

    /// <summary>
    /// Меню собирает карточки задач и общий цвет состояния из строк задач
    /// </summary>
    [Fact]
    public void The_tray_menu_lists_the_jobs_and_takes_the_worst_tone()
    {
        var model = new TrayMenuViewModel();

        model.Apply(new SyncSnapshot(), [new JobRow("mail", "Почта", "Не удалось", "", RowToneEnum.Problem, 0, true), new JobRow("reels", "Рилсы", "Ок", "", RowToneEnum.Ok, 0, true)]);

        Assert.Equal(3, model.Tasks.Count);
        Assert.Contains(model.Tasks, task => task.Name == "Почта");
        Assert.Equal(PanelToneEnum.Problem, model.Tone);
    }
}
