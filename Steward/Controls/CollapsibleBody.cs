using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Steward.Controls;

/// <summary>
/// Содержимое, которое плавно сворачивается и разворачивается. Внешний вид лежит в стиле <c>CollapsibleBody</c>, анимацию ведёт сам элемент: первое состояние ставится сразу, без анимации, а дальше высота и прозрачность меняются вместе
/// </summary>
public sealed class CollapsibleBody : ContentControl
{
    /// <summary>
    /// Развёрнуто ли содержимое
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(CollapsibleBody),
        new FrameworkPropertyMetadata(true, OnIsOpenChanged));

    /// <summary>
    /// Сжатие по высоте из шаблона
    /// </summary>
    private ScaleTransform? _scale;

    /// <summary>
    /// Обрезающая рамка с содержимым из шаблона
    /// </summary>
    private UIElement? _clip;

    /// <summary>
    /// Развёрнуто ли содержимое
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>
    /// Находит части шаблона и ставит начальное состояние без анимации
    /// </summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _scale = GetTemplateChild("Scale") as ScaleTransform;
        _clip = GetTemplateChild("Clip") as UIElement;
        Apply(animate: false);
    }

    /// <summary>
    /// Переводит содержимое в новое состояние
    /// </summary>
    /// <param name="d">Элемент</param>
    /// <param name="e">Старое и новое значение</param>
    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CollapsibleBody)d).Apply(animate: ((CollapsibleBody)d).IsLoaded);

    /// <summary>
    /// Ставит высоту и прозрачность по состоянию, плавно или сразу
    /// </summary>
    /// <param name="animate">
    /// <see langword="true"/>, если изменение нужно проиграть
    /// </param>
    private void Apply(bool animate)
    {
        if (_scale is null || _clip is null)
            return;

        var height = IsOpen ? 1.0 : 0.0;
        var opacity = IsOpen ? 1.0 : 0.0;
        if (!animate)
        {
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _clip.BeginAnimation(OpacityProperty, null);
            _scale.ScaleY = height;
            _clip.Opacity = opacity;

            return;
        }

        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(height, TimeSpan.FromSeconds(IsOpen ? 0.35 : 0.28))
        {
            EasingFunction = new CubicEase { EasingMode = IsOpen ? EasingMode.EaseOut : EasingMode.EaseInOut },
        });

        _clip.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromSeconds(IsOpen ? 0.3 : 0.18))
        {
            BeginTime = IsOpen ? TimeSpan.FromSeconds(0.08) : TimeSpan.Zero,
        });
    }
}
