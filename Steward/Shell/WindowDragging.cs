using System.Windows;
using Point = System.Windows.Point;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using System.Windows.Input;
using System.Windows.Media;

namespace Steward.Shell;

/// <summary>
/// Даёт тащить окно за шапку. Перетаскивание начинается, только когда мышь сдвинулась: обычный щелчок по шапке (по сфере, по значку) ничего не запускает. Если вызывать <c>DragMove</c> сразу при нажатии, быстрый щелчок оставляет окно в режиме перемещения, и анимации замирают до следующего движения мыши
/// </summary>
public static class WindowDragging
{
    /// <summary>
    /// Подключает перетаскивание
    /// </summary>
    /// <param name="window">Окно, которое двигается</param>
    /// <param name="handle">Шапка, за которую тянут</param>
    public static void Attach(Window window, UIElement handle)
    {
        Point? start = null;

        handle.PreviewMouseLeftButtonDown += (_, e) =>
        {
            start = e.OriginalSource is DependencyObject source && InsideButton(source, handle) ? null : e.GetPosition(window);
        };

        handle.PreviewMouseLeftButtonUp += (_, _) => start = null;
        handle.PreviewMouseMove += (_, e) =>
        {
            if (start is not { } origin || e.LeftButton != MouseButtonState.Pressed)
                return;

            var now = e.GetPosition(window);
            if (Math.Abs(now.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(now.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            start = null;
            window.DragMove();
        };
    }

    /// <summary>
    /// Проверяет, что нажатие пришлось на кнопку внутри шапки
    /// </summary>
    /// <param name="source">Элемент под мышью</param>
    /// <param name="handle">Шапка, выше которой не ищем</param>
    /// <returns>
    /// <see langword="true"/>, если элемент лежит внутри кнопки
    /// </returns>
    private static bool InsideButton(DependencyObject source, UIElement handle)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, handle); current = VisualTreeHelper.GetParent(current))
        {
            if (current is ButtonBase)
                return true;
        }

        return false;
    }
}
