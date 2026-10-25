using System.Windows;
using Steward.ViewModels;

namespace Steward.Views;

/// <summary>
/// Окно, где для каждого занятия выбирается формат домашнего задания
/// </summary>
public partial class HomeworkFormatsWindow : Window
{
    /// <summary>
    /// Создаёт окно
    /// </summary>
    /// <param name="model">Занятия с форматами</param>
    public HomeworkFormatsWindow(HomeworkFormatsViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}