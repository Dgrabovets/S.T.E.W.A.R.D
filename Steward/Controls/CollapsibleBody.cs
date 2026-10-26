using System.Windows;
using System.Windows.Controls;

namespace Steward.Controls;

/// <summary>
/// Содержимое, которое плавно сворачивается и разворачивается. Внешний вид и анимация лежат в стиле <c>CollapsibleBody</c>
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
        new FrameworkPropertyMetadata(true));

    /// <summary>
    /// Развёрнуто ли содержимое
    /// </summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }
}