using UserControl = System.Windows.Controls.UserControl;

namespace Steward.Views;

/// <summary>
/// Список занятий с выбором формата ДЗ и кнопкой подготовки заданий. Показывается и в окне, и на странице «Домашние задания»
/// </summary>
public partial class HomeworkFormatsView : UserControl
{
    /// <summary>
    /// Создаёт список
    /// </summary>
    public HomeworkFormatsView()
    {
        InitializeComponent();
    }
}