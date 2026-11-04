using System.Windows;
using Size = System.Windows.Size;

namespace Steward.Shell;

/// <summary>
/// Расчёты, чтобы окно без системной рамки помещалось в рабочую область монитора: где стоит развёрнутое окно, на сколько оно вылезло за край и какой наименьший размер ему можно разрешить. Только арифметика, без вызовов Windows, поэтому проверяется тестами
/// </summary>
public static class WindowFit
{
    /// <summary>
    /// Где и какого размера должно быть развёрнутое окно, в пикселях: ровно рабочая область монитора без панели задач. Положение отсчитывается от левого верхнего угла монитора, как ждёт Windows в <c>MINMAXINFO.ptMaxPosition</c>
    /// </summary>
    /// <param name="monitor">Весь монитор в пикселях экрана</param>
    /// <param name="workArea">Рабочая область монитора в пикселях экрана</param>
    /// <returns>Положение относительно монитора и размер развёрнутого окна</returns>
    public static Int32Rect MaximizedBounds(Int32Rect monitor, Int32Rect workArea) =>
        new(workArea.X - monitor.X, workArea.Y - monitor.Y, workArea.Width, workArea.Height);

    /// <summary>
    /// На сколько развёрнутое окно вылезло за рабочую область с каждой стороны, в единицах WPF. Окно без рамки Windows разворачивает шире экрана на толщину рамки; этот отступ возвращает содержимое в видимую часть. Если окно уже стоит ровно в рабочей области, отступы нулевые
    /// </summary>
    /// <param name="window">Прямоугольник окна в пикселях экрана</param>
    /// <param name="workArea">Рабочая область монитора в пикселях экрана</param>
    /// <param name="scaleX">Масштаб монитора по горизонтали: 1 при 100 %, 1.25 при 125 %</param>
    /// <param name="scaleY">Масштаб монитора по вертикали</param>
    /// <returns>Отступы слева, сверху, справа и снизу</returns>
    public static Thickness MaximizedInsets(Int32Rect window, Int32Rect workArea, double scaleX, double scaleY)
    {
        var x = scaleX > 0 ? scaleX : 1;
        var y = scaleY > 0 ? scaleY : 1;

        return new Thickness(
            Math.Max(0, workArea.X - window.X) / x,
            Math.Max(0, workArea.Y - window.Y) / y,
            Math.Max(0, (window.X + window.Width) - (workArea.X + workArea.Width)) / x,
            Math.Max(0, (window.Y + window.Height) - (workArea.Y + workArea.Height)) / y);
    }

    /// <summary>
    /// Наименьший размер окна, который помещается в рабочую область. Если задуманный наименьший размер больше экрана (маленький ноутбук или крупный масштаб), WPF раскладывал бы содержимое по задуманному размеру и обрезал низ окна
    /// </summary>
    /// <param name="designed">Задуманный наименьший размер в единицах WPF</param>
    /// <param name="workArea">Размер рабочей области в единицах WPF</param>
    /// <returns>Наименьший размер, не больше рабочей области</returns>
    public static Size MinimumSize(Size designed, Size workArea) =>
        new(Math.Max(0, Math.Min(designed.Width, workArea.Width)), Math.Max(0, Math.Min(designed.Height, workArea.Height)));

    /// <summary>
    /// Размер и место обычного окна: доля рабочей области по центру, не меньше наименьшего размера и не больше самой рабочей области
    /// </summary>
    /// <param name="workArea">Рабочая область в единицах WPF</param>
    /// <param name="minimum">Задуманный наименьший размер окна</param>
    /// <param name="share">Какую долю рабочей области занимает окно, например 0.9</param>
    /// <returns>Прямоугольник окна в единицах WPF</returns>
    public static Rect Fitted(Rect workArea, Size minimum, double share)
    {
        var floor = MinimumSize(minimum, workArea.Size);
        var width = Math.Min(workArea.Width, Math.Max(floor.Width, workArea.Width * share));
        var height = Math.Min(workArea.Height, Math.Max(floor.Height, workArea.Height * share));

        return new Rect(workArea.Left + ((workArea.Width - width) / 2), workArea.Top + ((workArea.Height - height) / 2), width, height);
    }
}
