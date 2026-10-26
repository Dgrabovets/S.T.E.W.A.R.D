using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Steward.Views;

namespace Steward.DevTools;

/// <summary>
/// Проверка быстрого статуса без иконки в трее на той последовательности событий, что бывает на самом деле: нажатие на иконку отбирает фокус у окна раньше, чем приходит щелчок
/// </summary>
public static class TrayCheck
{
    /// <summary>
    /// Проходит сценарии и пишет результат каждого
    /// </summary>
    /// <returns>Строки с отметкой «верно» или «неверно»</returns>
    public static string Run()
    {
        var menu = new TrayMenuWindow();
        var lines = new List<string>();
        var moves = new List<string>();
        menu.LocationChanged += (_, _) =>
        {
            if (menu.IsVisible)
                moves.Add($"{menu.Left:0},{menu.Top:0}");
        };

        var probe = (FrameworkElement)menu.Content; probe.Measure(new System.Windows.Size(menu.Width, double.PositiveInfinity)); lines.Add($"замер содержимого: {probe.DesiredSize.Width:0}x{probe.DesiredSize.Height:0}");
        // 1. Первый щелчок открывает окно, и оно не перескакивает с места на место
        Click(menu);
        Pump();
        var openAt = $"{menu.Left:0},{menu.Top:0} размер {menu.ActualWidth:0}x{menu.ActualHeight:0} курсор {System.Windows.Forms.Cursor.Position}";
        lines.Add(Verdict("первый щелчок открывает окно", menu.IsVisible));
        lines.Add(Verdict($"окно не переезжало после показа, пока оно видно (место {openAt}, перемещения {(moves.Count == 0 ? "нет" : string.Join(" -> ", moves))})", moves.Count == 0));

        // 2. Нажатие на иконку при открытом окне: система сначала отбирает фокус, потом приходит нажатие и щелчок. Окно должно остаться закрытым
        Deactivate(menu);
        menu.NotifyIconPressed();
        menu.ToggleFromIcon();
        Pump();
        lines.Add(Verdict("второй щелчок (фокус ушёл раньше) закрывает окно, а не открывает заново", !menu.IsVisible));

        // 3. То же, но событие нажатия пришло раньше потери фокуса
        System.Threading.Thread.Sleep(600);
        Click(menu);
        Pump();
        menu.NotifyIconPressed();
        menu.ToggleFromIcon();
        Pump();
        lines.Add(Verdict("второй щелчок (нажатие пришло раньше потери фокуса) закрывает окно", !menu.IsVisible));

        // 4. Следующий щелчок через время снова открывает
        System.Threading.Thread.Sleep(600);
        Click(menu);
        Pump();
        lines.Add(Verdict("щелчок по закрытому окну открывает его", menu.IsVisible));

        menu.Hide();

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Открывает быстрый статус и меряет, сколько процессора он ест, пока стоит на экране
    /// </summary>
    /// <returns>Загрузка в процентах одного ядра за 6 секунд</returns>
    public static string Measure()
    {
        var menu = new TrayMenuWindow();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        menu.Warm();
        Pump();
        watch.Restart();
        Click(menu);
        Pump();
        var firstOpen = watch.ElapsedMilliseconds;
        menu.Hide();
        System.Threading.Thread.Sleep(700);
        watch.Restart();
        Click(menu);
        Pump();
        var secondOpen = watch.ElapsedMilliseconds;
        var process = System.Diagnostics.Process.GetCurrentProcess();
        System.Threading.Thread.Sleep(1500);
        var before = process.TotalProcessorTime;
        var started = DateTime.UtcNow;
        while (DateTime.UtcNow - started < TimeSpan.FromSeconds(6))
        {
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Render);
            System.Threading.Thread.Sleep(8);
        }

        process.Refresh();
        var percent = (process.TotalProcessorTime - before).TotalSeconds / 6 * 100;
        menu.Hide();

        return $"первое открытие {firstOpen} мс, второе {secondOpen} мс; на экране ({(menu.AllowsTransparency ? "прозрачное окно" : "обычное окно")}) {percent:0.0}% одного ядра";
    }

    /// <summary>
    /// Нажатие и отпускание кнопки на иконке
    /// </summary>
    /// <param name="menu">Проверяемое окно</param>
    private static void Click(TrayPopupWindow menu)
    {
        menu.NotifyIconPressed();
        menu.ToggleFromIcon();
    }

    /// <summary>
    /// Имитирует потерю фокуса окном: так же, как это делает система при нажатии на панель задач
    /// </summary>
    /// <param name="menu">Проверяемое окно</param>
    private static void Deactivate(TrayPopupWindow menu) =>
        typeof(TrayPopupWindow).GetMethod("OnDeactivated", BindingFlags.NonPublic | BindingFlags.Instance, null, [typeof(object), typeof(EventArgs)], null)!.Invoke(menu, [null, EventArgs.Empty]);

    /// <summary>
    /// Прокачивает очередь окна, чтобы отработали отложенные действия
    /// </summary>
    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);

    /// <summary>
    /// Оформляет итог проверки
    /// </summary>
    /// <param name="what">Что проверялось</param>
    /// <param name="ok">Проверка прошла</param>
    /// <returns>Строка итога</returns>
    private static string Verdict(string what, bool ok) => $"{(ok ? "верно  " : "НЕВЕРНО")}: {what}";
}
