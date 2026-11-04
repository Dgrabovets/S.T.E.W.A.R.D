using System.Windows;
using Steward.Shell;
using Size = System.Windows.Size;

namespace Steward.Tests;

/// <summary>
/// Окно без системной рамки помещается в рабочую область монитора: развёрнутое не уходит под панель задач и за края, наименьший размер не больше экрана
/// </summary>
public class WindowFitTests
{
    /// <summary>
    /// Развёрнутое окно занимает рабочую область, а положение отсчитывается от угла своего монитора: панель задач снизу, слева, сверху и второй монитор левее основного
    /// </summary>
    /// <param name="monitorX">Левый край монитора</param>
    /// <param name="monitorY">Верхний край монитора</param>
    /// <param name="workX">Левый край рабочей области</param>
    /// <param name="workY">Верхний край рабочей области</param>
    /// <param name="workWidth">Ширина рабочей области</param>
    /// <param name="workHeight">Высота рабочей области</param>
    /// <param name="expectedX">Ожидаемое положение по горизонтали относительно монитора</param>
    /// <param name="expectedY">Ожидаемое положение по вертикали относительно монитора</param>
    [Theory]
    [InlineData(0, 0, 0, 0, 1920, 1032, 0, 0)]
    [InlineData(0, 0, 48, 0, 1872, 1080, 48, 0)]
    [InlineData(0, 0, 0, 48, 1920, 1032, 0, 48)]
    [InlineData(-1366, 200, -1366, 200, 1366, 728, 0, 0)]
    [InlineData(1920, -300, 1920, -252, 2560, 1392, 0, 48)]
    public void Maximized_bounds_are_the_work_area_relative_to_the_monitor(int monitorX, int monitorY, int workX, int workY, int workWidth, int workHeight, int expectedX, int expectedY)
    {
        var monitor = new Int32Rect(monitorX, monitorY, 1920, 1080);
        var bounds = WindowFit.MaximizedBounds(monitor, new Int32Rect(workX, workY, workWidth, workHeight));

        Assert.Equal(new Int32Rect(expectedX, expectedY, workWidth, workHeight), bounds);
    }

    /// <summary>
    /// Окно, которое Windows развернула шире экрана на рамку в 8 пикселей, получает отступы по 8 единиц при 100 % и по 8/1.25 при 125 %; окно точно в рабочей области отступов не получает
    /// </summary>
    [Fact]
    public void Insets_return_the_overflow_in_device_independent_units()
    {
        var work = new Int32Rect(0, 0, 1920, 1032);

        Assert.Equal(new Thickness(8), WindowFit.MaximizedInsets(new Int32Rect(-8, -8, 1936, 1048), work, 1, 1));
        Assert.Equal(new Thickness(0), WindowFit.MaximizedInsets(work, work, 1, 1));
        Assert.Equal(new Thickness(8 / 1.25), WindowFit.MaximizedInsets(new Int32Rect(-8, -8, 1936, 1048), work, 1.25, 1.25));

        var second = new Int32Rect(-1366, 0, 1366, 728);
        Assert.Equal(new Thickness(7, 7, 7, 47), WindowFit.MaximizedInsets(new Int32Rect(-1373, -7, 1380, 782), second, 1, 1));
    }

    /// <summary>
    /// Наименьший размер окна уменьшается до рабочей области маленького экрана и не растёт на большом
    /// </summary>
    [Fact]
    public void The_minimum_size_never_exceeds_the_work_area()
    {
        var designed = new Size(900, 680);

        Assert.Equal(new Size(900, 680), WindowFit.MinimumSize(designed, new Size(1920, 1032)));
        Assert.Equal(new Size(900, 576), WindowFit.MinimumSize(designed, new Size(1093, 576)));
        Assert.Equal(new Size(853, 442), WindowFit.MinimumSize(designed, new Size(853, 442)));
    }

    /// <summary>
    /// Обычное окно занимает 90 % рабочей области по центру, а на маленьком экране не выходит за неё, даже если наименьший размер из разметки больше
    /// </summary>
    [Fact]
    public void A_fitted_window_stays_inside_the_work_area()
    {
        var large = WindowFit.Fitted(new Rect(0, 0, 1920, 1032), new Size(900, 680), 0.9);
        Assert.Equal(96, large.Left, 6);
        Assert.Equal(51.6, large.Top, 6);
        Assert.Equal(1728, large.Width, 6);
        Assert.Equal(928.8, large.Height, 6);

        var small = WindowFit.Fitted(new Rect(0, 0, 1093, 576), new Size(900, 680), 0.9);
        Assert.Equal(576, small.Height);
        Assert.Equal(0, small.Top);
        Assert.True(small.Right <= 1093 && small.Bottom <= 576);

        var shifted = WindowFit.Fitted(new Rect(-1366, 0, 1366, 728), new Size(900, 680), 0.9);
        Assert.True(shifted.Left >= -1366 && shifted.Right <= 0 && shifted.Bottom <= 728);
    }
}
