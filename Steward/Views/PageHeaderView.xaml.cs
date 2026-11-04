using System.Windows;

namespace Steward.Views;

/// <summary>
/// Шапка страницы процесса: название, плашки состояния и кнопка, заменяющая содержимое страницы настройками
/// </summary>
public partial class PageHeaderView : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Название страницы
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeaderView), new PropertyMetadata(string.Empty));

    /// <summary>
    /// Создаёт шапку
    /// </summary>
    public PageHeaderView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Название страницы
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
