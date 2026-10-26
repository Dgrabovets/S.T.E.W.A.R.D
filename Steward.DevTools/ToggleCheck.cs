using Steward.Views;

namespace Steward.DevTools;

/// <summary>
/// Проверка быстрого статуса без иконки в трее: щелчок открывает окно, второй закрывает, третий открывает снова
/// </summary>
public static class ToggleCheck
{
    /// <summary>
    /// Нажимает «иконку» три раза подряд и пишет, видно ли окно после каждого щелчка
    /// </summary>
    /// <returns>Строка вида «открыт, закрыт, открыт» и вердикт</returns>
    public static string Run()
    {
        var menu = new TrayMenuWindow();
        var states = new List<bool>();
        for (var click = 0; click < 3; click++)
        {
            menu.ToggleFromIcon();
            menu.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            states.Add(menu.IsVisible);
        }

        menu.Hide();

        return $"после щелчков окно: {string.Join(", ", states.Select(visible => visible ? "открыто" : "закрыто"))}. {(states.SequenceEqual([true, false, true]) ? "Верно" : "Неверно")}";
    }
}
