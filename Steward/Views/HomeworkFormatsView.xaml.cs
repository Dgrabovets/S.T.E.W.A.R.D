using System.Windows;
using Steward.ViewModels;
using UserControl = System.Windows.Controls.UserControl;

namespace Steward.Views;

/// <summary>
/// Вкладка «Автоматизация образования»: плитки предметов, расшифровки и домашние задания. Окна «Методические указания» и «Подготовить задание» открываются из неё поверх окна приложения
/// </summary>
public partial class HomeworkFormatsView : UserControl
{
    /// <summary>
    /// Создаёт вкладку
    /// </summary>
    public HomeworkFormatsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// Подписывается на просьбы открыть окна у новой модели и отписывается от прежней
    /// </summary>
    /// <param name="sender">Вкладка</param>
    /// <param name="e">Старая и новая модель</param>
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is HomeworkFormatsViewModel old)
        {
            old.GuidesDialogRequested -= ShowGuides;
            old.PrepareDialogRequested -= ShowPrepare;
        }

        if (e.NewValue is HomeworkFormatsViewModel current)
        {
            current.GuidesDialogRequested += ShowGuides;
            current.PrepareDialogRequested += ShowPrepare;
        }
    }

    /// <summary>
    /// Открывает окно «Методические указания» и после закрытия обновляет сводку
    /// </summary>
    private void ShowGuides()
    {
        if (DataContext is not HomeworkFormatsViewModel model)
            return;

        GuidesWindow.Show(Window.GetWindow(this), model.CreateGuidesDialog());
        model.RefreshGuides();
    }

    /// <summary>
    /// Открывает окно «Подготовить задание» и, если выбор подтверждён, запускает подготовку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private async void ShowPrepare(LessonFormatViewModel lesson)
    {
        if (DataContext is not HomeworkFormatsViewModel model)
            return;

        var dialog = model.CreatePrepareDialog(lesson);
        if (PrepareWindow.Ask(Window.GetWindow(this), dialog))
            await lesson.PrepareAsync(dialog.Request());
    }
}
