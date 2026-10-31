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
            old.SubjectGuidesRequested -= ShowSubjectGuides;
            old.RedoDialogRequested -= ShowRedo;
            old.DeleteConfirmRequested -= ConfirmDelete;
            old.AttachFileRequested -= AttachWork;
            old.SubmittedFileRequested -= ChooseSubmittedFile;
            old.ConditionEditRequested -= EditCondition;
        }

        if (e.NewValue is HomeworkFormatsViewModel current)
        {
            current.GuidesDialogRequested += ShowGuides;
            current.PrepareDialogRequested += ShowPrepare;
            current.SubjectGuidesRequested += ShowSubjectGuides;
            current.RedoDialogRequested += ShowRedo;
            current.DeleteConfirmRequested += ConfirmDelete;
            current.AttachFileRequested += AttachWork;
            current.SubmittedFileRequested += ChooseSubmittedFile;
            current.ConditionEditRequested += EditCondition;
        }
    }

    /// <summary>
    /// Раскрывает меню «…» задания под кнопкой: так оно открывается и мышью, и с клавиатуры (Enter или пробел)
    /// </summary>
    /// <param name="sender">Кнопка «…»</param>
    /// <param name="e">Событие</param>
    private void ItemMenu_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { ContextMenu: { } menu } button)
            return;

        // Меню живёт во всплывающем окне: модель задания передаётся явно, чтобы команды пунктов точно её получили
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    /// <summary>
    /// Спрашивает файл своей готовой работы и прикрепляет его к заданию
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="item">Задание</param>
    private async void AttachWork(LessonFormatViewModel lesson, LessonItemViewModel item)
    {
        try
        {
            if (DataContext is HomeworkFormatsViewModel model && PickFile($"Своя готовая работа: задание {item.Number}") is { } file)
                await model.AttachWorkAsync(lesson, item.Number, file);
        }
        catch (Exception exception)
        {
            // Обработчик без ожидания: ошибка не должна уронить окно
            Fail(lesson, "Не удалось прикрепить работу: ", exception);
        }
    }

    /// <summary>
    /// Пишет ошибку действия в журнал и показывает её в карточке занятия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="prefix">Начало сообщения: что не удалось</param>
    /// <param name="exception">Ошибка</param>
    private static void Fail(LessonFormatViewModel lesson, string prefix, Exception exception)
    {
        System.Diagnostics.Trace.TraceError($"{prefix}{lesson.Id}: {exception}");
        lesson.ShowResult(prefix + exception.Message, failed: true);
    }

    /// <summary>
    /// Спрашивает файл сданной работы и делает его образцом предмета вместо черновика
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="item">Задание</param>
    private async void ChooseSubmittedFile(LessonFormatViewModel lesson, LessonItemViewModel item)
    {
        try
        {
            if (DataContext is HomeworkFormatsViewModel model && PickFile($"Файл сданной работы: задание {item.Number}") is { } file)
                await model.SetSubmittedFileAsync(lesson, item.Number, file);
        }
        catch (Exception exception)
        {
            // Обработчик без ожидания: ошибка не должна уронить окно
            Fail(lesson, "Не удалось записать сданную работу: ", exception);
        }
    }

    /// <summary>
    /// Открывает окно «Поправить условие» и сохраняет правку
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <param name="item">Задание</param>
    private async void EditCondition(LessonFormatViewModel lesson, LessonItemViewModel item)
    {
        try
        {
            if (DataContext is HomeworkFormatsViewModel model && ConditionWindow.Ask(Window.GetWindow(this), lesson.Meta, item.Title, item.FullRequirements) is { } answer)
                await model.FixConditionAsync(lesson, item.Number, answer.Title, answer.Requirements);
        }
        catch (Exception exception)
        {
            // Обработчик без ожидания: ошибка не должна уронить окно
            Fail(lesson, "Не удалось поправить условие: ", exception);
        }
    }

    /// <summary>
    /// Показывает окно выбора одного файла
    /// </summary>
    /// <param name="title">Заголовок окна</param>
    /// <returns>Путь к файлу или <see langword="null"/>, если выбор отменён</returns>
    private string? PickFile(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = "Работы|*.docx;*.pptx;*.pdf;*.md;*.txt;*.zip;*.py;*.cs;*.ipynb|Все файлы|*.*",
            FilterIndex = 2,
        };

        return Window.GetWindow(this) is { } owner ? dialog.ShowDialog(owner) == true ? dialog.FileName : null : dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Открывает окно «Переделать задание» и, если записаны замечания, запоминает замечание и запускает подготовку заново. Ошибки ловятся внутри <see cref="HomeworkFormatsViewModel.RedoAsync"/> и подготовки, остальные пишутся в журнал и показываются в карточке
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private async void ShowRedo(LessonFormatViewModel lesson)
    {
        try
        {
            if (DataContext is HomeworkFormatsViewModel model && RedoWindow.Ask(Window.GetWindow(this), lesson.Meta) is { } answer)
                await model.RedoAsync(lesson, FormatOfDrafts(lesson), answer.Text, answer.Files, answer.Kind, answer.Global);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Переделка {lesson.Id}: {exception}");
            lesson.ShowResult("Переделка не удалась: " + exception.Message, failed: true);
        }
    }

    /// <summary>
    /// Формат готовой работы по расширению её файла: переделка идёт в том же формате, что и прежняя версия
    /// </summary>
    /// <param name="lesson">Занятие</param>
    /// <returns>Формат прежней работы или формат занятия, если по файлам он не ясен</returns>
    private static Steward.Homework.Models.HomeworkFormatEnum FormatOfDrafts(LessonFormatViewModel lesson) =>
        System.IO.Path.GetExtension(lesson.Drafts.FirstOrDefault()?.Path ?? string.Empty).ToLowerInvariant() switch
        {
            ".docx" => Steward.Homework.Models.HomeworkFormatEnum.Word,
            ".pptx" => Steward.Homework.Models.HomeworkFormatEnum.Presentation,
            ".txt" or ".md" => Steward.Homework.Models.HomeworkFormatEnum.Text,
            { Length: > 0 } => Steward.Homework.Models.HomeworkFormatEnum.Code,
            _ => lesson.Format,
        };

    /// <summary>
    /// Спрашивает, удалять ли готовое задание, и удаляет после подтверждения
    /// </summary>
    /// <param name="lesson">Занятие</param>
    private void ConfirmDelete(LessonFormatViewModel lesson)
    {
        if (DataContext is HomeworkFormatsViewModel model && ConfirmWindow.Ask("Удалить готовое по занятию?", $"{lesson.Meta}: будут удалены все готовые работы этого занятия, их прежние версии и отчёт проверки. Задания можно будет подготовить заново.", "Удалить", "Отмена"))
            model.DeleteDrafts(lesson);
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
    /// Открывает окно «Уточнить для предмета» и запоминает, какие указания и образцы к предмету не относятся
    /// </summary>
    /// <param name="subject">Предмет</param>
    private void ShowSubjectGuides(SubjectViewModel subject)
    {
        try
        {
            if (DataContext is HomeworkFormatsViewModel model && SubjectGuidesWindow.Ask(Window.GetWindow(this), model.CreateSubjectGuidesDialog(subject)) is { } excluded)
                subject.Options?.SetExcludedGuides(excluded);
        }
        catch (Exception exception)
        {
            // Обработчик без ожидания: ошибка не должна уронить окно
            System.Diagnostics.Trace.TraceError($"Не удалось открыть выбор указаний предмета {subject.Title}: {exception}");
        }
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
