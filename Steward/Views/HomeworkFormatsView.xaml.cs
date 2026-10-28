using Steward.Homework.Models;
using Steward.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace Steward.Views;

/// <summary>
/// Вкладка «Автоматизация образования»: плитки предметов, расшифровки и домашние задания с методическими указаниями и предпросмотром
/// </summary>
public partial class HomeworkFormatsView : UserControl
{
    /// <summary>
    /// Создаёт вкладку
    /// </summary>
    public HomeworkFormatsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Выбирает файлы методических указаний
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void AddGuideFiles_Click(object sender, System.Windows.RoutedEventArgs e) => Pick(GuideKindEnum.Guide);

    /// <summary>
    /// Выбирает файлы образцов
    /// </summary>
    /// <param name="sender">Кнопка</param>
    /// <param name="e">Событие</param>
    private void AddExampleFiles_Click(object sender, System.Windows.RoutedEventArgs e) => Pick(GuideKindEnum.Example);

    /// <summary>
    /// Открывает окно выбора файлов и передаёт выбранное модели
    /// </summary>
    /// <param name="kind">Указания или образцы</param>
    private void Pick(GuideKindEnum kind)
    {
        if (DataContext is not HomeworkFormatsViewModel model)
            return;

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = kind == GuideKindEnum.Guide ? "Методические указания" : "Образцы работ",
            Filter = "Документы и текст|*.docx;*.pptx;*.pdf;*.md;*.txt;*.rtf|Все файлы|*.*",
        };
        if (dialog.ShowDialog() == true)
            model.AddFiles(kind, dialog.FileNames);
    }
}
