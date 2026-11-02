namespace Steward.Views;

/// <summary>
/// Всплывающее окошко внизу приложения: текст, кнопка «Откатить» с значком и кнопка «Закрыть» с значком
/// </summary>
public partial class ToastView : System.Windows.Controls.UserControl
{
    /// <summary>
    /// Создаёт окошко
    /// </summary>
    public ToastView()
    {
        InitializeComponent();
    }
}
