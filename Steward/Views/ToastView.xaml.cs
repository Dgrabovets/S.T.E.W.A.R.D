using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Animation;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Всплывающее окошко внизу приложения: текст, кольцо с числом секунд, круглая кнопка «Откатить» и «Закрыть». Заливка и кольцо убывают анимацией окна, а не обновлением из модели, поэтому идут плавно
/// </summary>
public partial class ToastView : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Длина дуги кольца в толщинах штриха: кольцо диаметром 35 и толщиной 3
    /// </summary>
    private const double RING_LENGTH = 36.65;

    /// <summary>
    /// Создаёт окошко
    /// </summary>
    public ToastView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Подписывается на показы новой модели и отписывается от прежней
    /// </summary>
    /// <param name="sender">Окошко</param>
    /// <param name="e">Старая и новая модель</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ToastViewModel old)
            old.PropertyChanged -= OnModelChanged;

        if (e.NewValue is ToastViewModel current)
            current.PropertyChanged += OnModelChanged;
    }

    /// <summary>
    /// При новом показе запускает заново плавное убывание заливки и кольца на всё время отсчёта
    /// </summary>
    /// <param name="sender">Модель окошка</param>
    /// <param name="e">Какое свойство изменилось</param>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ToastViewModel.Version) || sender is not ToastViewModel model)
            return;

        var length = new Duration(TimeSpan.FromMilliseconds(model.TotalSeconds * model.TickMilliseconds));
        DrainScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0, length));
        RingArc.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty, new DoubleAnimation(0, RING_LENGTH, length));
    }
}
