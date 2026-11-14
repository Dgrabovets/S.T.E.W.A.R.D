using System.Windows;
using System.Windows.Controls;
using Panel = System.Windows.Controls.Panel;
using Size = System.Windows.Size;

namespace Steward.Controls;

/// <summary>
/// Панель, которая переносит элементы на новые строки, как WrapPanel, но каждую строку ставит по центру ширины
/// </summary>
public sealed class CenteredWrapPanel : Panel
{
    /// <summary>
    /// Меряет элементы и раскладывает их по строкам с учётом доступной ширины
    /// </summary>
    /// <param name="availableSize">Доступное место</param>
    /// <returns>Ширина самой длинной строки и общая высота строк</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var row in Rows(availableSize.Width, measure: true))
        {
            width = Math.Max(width, row.Width);
            height += row.Height;
        }

        return new Size(double.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    /// <summary>
    /// Ставит каждую строку по центру ширины
    /// </summary>
    /// <param name="finalSize">Отведённое место</param>
    /// <returns>Занятое место</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = 0.0;
        foreach (var row in Rows(finalSize.Width, measure: false))
        {
            var left = Math.Max(0, (finalSize.Width - row.Width) / 2);
            foreach (var child in row.Items)
            {
                child.Arrange(new Rect(left, top, child.DesiredSize.Width, row.Height));
                left += child.DesiredSize.Width;
            }

            top += row.Height;
        }

        return finalSize;
    }

    /// <summary>
    /// Делит элементы на строки по ширине
    /// </summary>
    /// <param name="maxWidth">Ширина, в которую строка должна поместиться</param>
    /// <param name="measure">Нужно ли сначала замерить элементы</param>
    /// <returns>Строки: элементы, их общая ширина и высота</returns>
    private List<(List<UIElement> Items, double Width, double Height)> Rows(double maxWidth, bool measure)
    {
        var rows = new List<(List<UIElement>, double, double)>();
        var items = new List<UIElement>();
        double width = 0, height = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (measure)
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            var size = child.DesiredSize;
            if (items.Count > 0 && width + size.Width > maxWidth)
            {
                rows.Add((items, width, height));
                items = [];
                width = 0;
                height = 0;
            }

            items.Add(child);
            width += size.Width;
            height = Math.Max(height, size.Height);
        }

        if (items.Count > 0)
            rows.Add((items, width, height));

        return rows;
    }
}
